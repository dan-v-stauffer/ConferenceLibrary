using System;
using System.IO;
using System.Text;
using System.Web;

// Create our own utility for exceptions
namespace ConferenceLibrary
{
    public sealed class ExceptionUtility
    {
        // All methods are static, so this can be private
        private ExceptionUtility()
        { }

        // Log an Exception
        public static void LogException(Exception exc, string source)
        {
            // Include enterprise logic for logging exceptions
            // Get the absolute path to the log file

            string logFile = HttpContext.Current.Server.MapPath(@"~/App_Data/ErrorLog.txt");
            string userName = string.Empty;

            if (HttpContext.Current.Session != null)
            {
                if (HttpContext.Current.Session["rsvpUser"] != null)
                {
                    userName = ((ConferenceLibrary.ConferenceUser)HttpContext.Current.Session["rsvpUser"]).Email;
                }
            }

            // Open the log file for append and write the log
            StreamWriter sw = new StreamWriter(logFile, true);
            sw.WriteLine("********** {0} **********", DateTime.Now);
            if (userName != string.Empty)
                sw.WriteLine("******** {0} **********", userName);
            if (exc.InnerException != null)
            {

                sw.Write("Inner Exception Type: ");
                sw.WriteLine(exc.InnerException.GetType().ToString());
                sw.Write("Inner Exception: ");
                sw.WriteLine(exc.InnerException.Message);
                sw.Write("Inner Source: ");
                sw.WriteLine(exc.InnerException.Source);
                if (exc.InnerException.StackTrace != null)
                {
                    sw.WriteLine("Inner Stack Trace: ");
                    sw.WriteLine(exc.InnerException.StackTrace);
                }
            }
            sw.Write("Exception Type: ");
            sw.WriteLine(exc.GetType().ToString());
            sw.WriteLine("Exception: " + exc.Message);
            sw.WriteLine("Source: " + source);
            sw.WriteLine("Stack Trace: ");
            if (exc.StackTrace != null)
            {
                sw.WriteLine(exc.StackTrace);
                sw.WriteLine();
            }
            sw.Close();
        }

        // Notify System Operators about an exception
        public static void NotifySystemOps(Exception exc, string source)
        {
            try
            {
                if (exc is System.Net.Mail.SmtpException)
                    return;

                StringBuilder sw = new StringBuilder();
                string userName = string.Empty;

                if (HttpContext.Current.Session != null)
                {
                    if (HttpContext.Current.Session["rsvpUser"] != null)
                    {
                        userName = ((ConferenceLibrary.ConferenceUser)HttpContext.Current.Session["rsvpUser"]).Email;
                    }
                }

                sw.AppendLine("********** {0} **********" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString());
                if (userName != string.Empty)
                    sw.AppendLine("******** " + userName + " **********");


                if (exc.InnerException != null)
                {
                    sw.Append("Inner Exception Type: ");
                    sw.AppendLine(exc.InnerException.GetType().ToString());
                    sw.Append("Inner Exception: ");
                    sw.AppendLine(exc.InnerException.Message);
                    sw.Append("Inner Source: ");
                    sw.AppendLine(exc.InnerException.Source);
                    if (exc.InnerException.StackTrace != null)
                    {
                        sw.AppendLine("Inner Stack Trace: ");
                        sw.AppendLine(exc.InnerException.StackTrace);
                    }
                }
                sw.Append("Exception Type: ");
                sw.AppendLine(exc.GetType().ToString());
                sw.AppendLine("Exception: " + exc.Message);
                sw.AppendLine("Source: " + source);
                sw.AppendLine("Stack Trace: ");
                if (exc.StackTrace != null)
                {
                    sw.AppendLine(exc.StackTrace);
                    sw.AppendLine();
                }


                ConferenceLibrary.Email errEmail = new ConferenceLibrary.Email(
                    System.Configuration.ConfigurationManager.AppSettings["sysadmin"],
                    sw.ToString(), "Engineering Conference RSVP Website Error");

                errEmail.Send();
            }
            catch (Exception e)
            {
                LogException(e, "App_Code/ExceptionUtility.cs.NotifySystemOps(Exception exc, string source)");
            }
        }
    }
}//end namespace ConferenceLibrary
 