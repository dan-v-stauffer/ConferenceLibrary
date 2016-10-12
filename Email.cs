using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Web;

/// <summary>
/// Summary description for Email
/// </summary>
/// 
/// 
namespace ConferenceLibrary
{
    public class Email
    {

        private string _from = "EngineeringConference@kla-tencor.com";
        private List<string> _toAddressees = new List<string>();
        private List<string> _ccAddressees = new List<string>();
        private string _body;
        private string _subject;
        private List<System.Net.Mail.Attachment> _attachments = new List<System.Net.Mail.Attachment>();
        private List<string> _bccAddressees = new List<string>();
        private SmtpClient _client = new SmtpClient(System.Configuration.ConfigurationManager.AppSettings["mailserver"]);
        private System.Net.Mail.MailPriority _priority = MailPriority.Normal;
        private List<AlternateView> _altViews = new List<AlternateView>();

        public Email(string toAddressee, string Body, string Subject)
        {
            _toAddressees.Add(toAddressee);
            _body = Body;
            _subject = Subject;
            _bccAddressees.Add(System.Configuration.ConfigurationManager.AppSettings["sysadmin"] );
        }

        public Email(List<string> toAddressees, string Body, string Subject)
        {
            _toAddressees = toAddressees;
            _body = Body;
            _subject = Subject;
        }

        public Email(string toAddressee, string ccAddressee, string Body, string Subject)
            : this(toAddressee, Body, Subject)
        {
            _ccAddressees.Add(ccAddressee);
            _body = Body;
            _subject = Subject;
        }

        public Email(List<string> toAddressees, List<string> ccAddressees, string Body, string Subject)
            : this(toAddressees, Body, Subject)
        {
            _ccAddressees = ccAddressees;
        }

        public void AddToAddressee(string toAddressee)
        {
            _toAddressees.Add(toAddressee);
        }

        public void AddCCAddressee(string ccAddressee)
        {
            _ccAddressees.Add(ccAddressee);
        }

        public void AddBccAddressee(string bccAddressee)
        {
            _bccAddressees.Add(bccAddressee);
        }
        public void AddToAddressee(List<string> toAddressees)
        {
            foreach (string emailAddress in toAddressees)
                _toAddressees.Add(emailAddress);
        }

        public void AddCCAddressee(List<string> ccAddressees)
        {
            foreach (string emailAddress in ccAddressees)
                _ccAddressees.Add(emailAddress);
        }

        public void AddBccAddressee(List<string> bccAddressees)
        {
            foreach (string emailAddress in bccAddressees)
                _bccAddressees.Add(emailAddress);
        }

        public void UpdateSubject(string subject)
        {
            _subject = subject;
        }

        public void AddAttachment(string filePath)
        {
            _attachments.Add(new System.Net.Mail.Attachment(filePath));
        }

        public void AddAttachments(List<string> filePaths)
        {
            foreach (string filePath in filePaths)
                _attachments.Add(new System.Net.Mail.Attachment(filePath));
        }

        private void AddAttachmentFromString(string attachment)
        {

        }

        public void SetPriority(MailPriority priority)
        {
            _priority = priority;
        }

        public void SendAlternate()
        {

            MailMessage msg = new MailMessage();
            msg.From = new MailAddress("norman1.cheung@kla-tencor.com", "Norman Cheung");
            addAddressToMailMessage(msg.To, _toAddressees);

            msg.Body = _body;
            msg.Priority = _priority;
            _client.Timeout = 20000;
            _client.Send(msg);
            _client.Dispose();
            msg.Dispose();
        }

        public void Send()
        {
            if (System.Configuration.ConfigurationManager.AppSettings["SendEmail"] != "true")
                return;

            try
            {
                ExceptionUtility.LogException(new Exception(), "INSIDE Email.Send()");

                MailMessage msg = new MailMessage();
                msg.From = new MailAddress(_from, "Engineering Conference: Accelerate Innovation");
                addAddressToMailMessage(msg.To, _toAddressees);
                addAddressToMailMessage(msg.CC, _ccAddressees);
                addAddressToMailMessage(msg.Bcc, _bccAddressees);

                msg.Body = _body;
                msg.IsBodyHtml = true;
                msg.Subject = _subject;
                msg.Priority = _priority;

                if (_altViews.Count > 0)
                    foreach (AlternateView item in _altViews)
                        msg.AlternateViews.Add(item);

                if (_attachments.Count > 0)
                    foreach (System.Net.Mail.Attachment attachment in _attachments)
                        msg.Attachments.Add(attachment);
                _client.Timeout = 20000;
                _client.Send(msg);
                _client.Dispose();
                msg.Dispose();
            }
            catch (System.Net.Mail.SmtpException e)
            {
                ExceptionUtility.LogException(e, "ConferenceLibrary.Email.Send()");
            }
        }

        public void Send(MailMessage msg)
        {
            try
            {
                if (System.Configuration.ConfigurationManager.AppSettings["SendEmail"] != "true")
                    return;
                msg.From = new MailAddress(_from, "Engineering Conference: Accelerate Innovation");
                _client.Timeout = 20000;
                _client.Send(msg);
                _client.Dispose();
            }
            catch (System.Net.Mail.SmtpException e)
            {
                ExceptionUtility.LogException(e, "ConferenceLibrary.Email.Send(MailMessage msg)");
            }
        }

        public void SendZainEmail()
        {
            if (System.Configuration.ConfigurationManager.AppSettings["SendEmail"] != "true")
                return;

            try
            {
                MailMessage msg = new MailMessage();
                msg.From = new MailAddress(_from, "Saidin, Zain");
                addAddressToMailMessage(msg.To, _toAddressees);
                addAddressToMailMessage(msg.CC, _ccAddressees);
                addAddressToMailMessage(msg.Bcc, _bccAddressees);

                msg.Body = _body;
                msg.IsBodyHtml = true;
                msg.Subject = _subject;
                msg.Priority = _priority;

                if (_altViews.Count > 0)
                    foreach (AlternateView item in _altViews)
                        msg.AlternateViews.Add(item);

                if (_attachments.Count > 0)
                    foreach (System.Net.Mail.Attachment attachment in _attachments)
                        msg.Attachments.Add(attachment);
                _client.Timeout = 20000;
                _client.Send(msg);
                _client.Dispose();
                msg.Dispose();
            }
            catch (System.Net.Mail.SmtpException e)
            {
                ExceptionUtility.LogException(e, "ConferenceLibrary.Email.SendZainEmail()");
            }
        }

        private void addAddressToMailMessage(MailAddressCollection coll, List<string> list)
        {
            foreach (string str in list)
                coll.Add(new MailAddress(str));
        }

        public void AddAttachmentFromString(string content, string title)
        {
            _attachments.Add(System.Net.Mail.Attachment.CreateAttachmentFromString(content, title));
        }

        public void AddAlternativeView(AlternateView view)
        {
            _altViews.Add(view);
        }
    }
}