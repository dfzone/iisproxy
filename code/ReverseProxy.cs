using System;
using System.Configuration;
using System.Web; 
using System.Net;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace ReverseProxy
{
	public class ReverseProxy: IHttpHandler
	{	
	    EventLog event_log;	    
	    public ReverseProxy()
	    {
            // Create the event logging stream		    
            string source = "iisproxy";
            if (!EventLog.SourceExists(source)) {
                EventLog.CreateEventSource(source, "Application");
            }
            event_log = new EventLog();
            event_log.Source = source;
	    }
	    
		public void ProcessRequest(HttpContext context)
		{			    		
			// Create the web request to communicate with the back-end site
			string remoteUrl = ConfigurationSettings.AppSettings["ProxyUrl"] + 
			        context.Request.Path + "?" + context.Request.QueryString;
			HttpWebRequest request = (HttpWebRequest) WebRequest.Create(remoteUrl);
			request.AllowAutoRedirect = false;
			request.Method = context.Request.HttpMethod;
			request.ContentType = context.Request.ContentType;
            request.Headers["Remote-User"] = HttpContext.Current.User.Identity.Name;
            foreach(String each in context.Request.Headers)
                if (!WebHeaderCollection.IsRestricted(each) && each != "Remote-User") 
                    request.Headers.Add(each, context.Request.Headers.Get(each));           
            if (context.Request.HttpMethod == "POST")
            {
                Stream outputStream = request.GetRequestStream();
                CopyStream(context.Request.InputStream, outputStream);
                outputStream.Close();
            }

			HttpWebResponse response;
			try
			{
				response = (HttpWebResponse) request.GetResponse();
			}
			catch(System.Net.WebException we)
			{
			    response = (HttpWebResponse) we.Response;
			    // TBD: handle case where we.Response is null
			}

            // Copy response from server back to client
            context.Response.StatusCode = (int) response.StatusCode;
            context.Response.StatusDescription = response.StatusDescription;
            if(response.Headers.Get("Location") != null)                
            {   
                if(ConfigurationSettings.AppSettings.Get("traceRedirect") != null)
                    event_log.WriteEntry("Back-end redirecting to: " + response.Headers.Get("Location"), EventLogEntryType.Information);
                context.Response.AddHeader("Location", context.Request.Url.GetLeftPart(UriPartial.Authority) +
                    response.Headers.Get("Location").Substring(ConfigurationSettings.AppSettings["ProxyUrl"].Length));
            }
            foreach(String each in response.Headers)
                if(each != "Location")
                    context.Response.AddHeader(each, response.Headers.Get(each));           
			CopyStream(response.GetResponseStream(), context.Response.OutputStream);
			response.Close();
			context.Response.End();
		}
		
        static public void CopyStream(Stream input, Stream output) {
            Byte[] buffer = new byte[1024];
            int bytes = 0;
            while( (bytes = input.Read(buffer, 0, 1024) ) > 0 )
                output.Write(buffer, 0, bytes);
        }
 
		public bool IsReusable
		{
			get
			{
				return true;
			}
		}	
	}
}
