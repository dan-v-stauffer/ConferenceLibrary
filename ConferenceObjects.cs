using DataUtilities;
using DataUtilities.SQLServer;
using DataUtilities.KTActiveDirectory;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI;
using System.Xml;
using HelperFunctions;
using DDay.iCal;
using DDay.Collections;
using DDay.iCal.Serialization.iCalendar;
using System.Resources;
/// <summary>
/// Summary description for RSVPObject
/// </summary>
/// 
namespace ConferenceLibrary
{
    public class EventItem
    {
        public int EventID;
        public int ParentEventID;
        public String UserRole;
        public int RequestOrder;
        public bool Assigned;

        public EventItem(int eventID, int parentEventID,
            string userRole, int requestOrder, bool eventAssigned)
        {
            EventID = eventID;
            ParentEventID = parentEventID;
            UserRole = userRole;
            RequestOrder = requestOrder;
            Assigned = eventAssigned;
        }
    }

    public class MealItem
    {
        public int MealID;
        public int MealOptionID;

        public MealItem(int mealID, int mealOptionID)
        {
            MealID = mealID;
            MealOptionID = mealOptionID;
        }
    }

    public class TransportationItem
    {
        public int TransporationModeID;
        public string TransporationDirection;
        public TransportationItem(int modeID, string direction)
        {
            TransporationModeID = modeID;
            TransporationDirection = direction;
        }
    }

    public class RSVP : INotifyPropertyChanged
    {
        //using nullable dates since some date values may be == DBNull.Value

        private ConferenceUser _user = null;
        private String _invitationType = string.Empty;
        private DateTime _registrationDate;
        private DateTime? _confirmDate;
        private DateTime? _cancelDate;
        private DateTime? _checkInDate;
        private DateTime? _checkOutDate;
        private Boolean _welcomeReception = true;
        private Boolean _golfing = false;
        private ConferenceUser _admin = null;
        private String _rsvpNotes = string.Empty;
        private Boolean _photoWaiver = true;
        private Boolean _validRSVP = false;
        private String _confirmationCode = String.Empty;

        private DataTable _events = new DataTable("events");
        private DataTable _meals = new DataTable("meals");
        private DataTable _transportation = new DataTable("transportation");
        private DateTime _objTimeStamp;

        private RSVP _origRSVP = null;

        private string _conferenceYear = Convert.ToDateTime(System.Configuration.ConfigurationManager.AppSettings["ConferenceStart"]).ToString("yyyy");

        private WebDataUtility dataUtil = WebDataUtility.Instance;

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public ConferenceUser User
        {
            get { return _user; }
            set
            {
                _user = value;
            }
        }
        public String ConfirmationCode
        {
            get
            {
                if (_confirmationCode.Length == 0 || _confirmationCode.Equals(DBNull.Value))
                {
                    _confirmationCode = generateConfirmationCode();
                    Update();
                }
                return _confirmationCode;
            }
        }
        public String InvitationType
        {
            get { return _invitationType; }
        }
        public DateTime RegisistrationDate
        {
            get { return _registrationDate; }
        }
        public DateTime? ConfirmDate
        {
            get { return _confirmDate; }
            set
            {
                _confirmDate = value;
                OnPropertyChanged();
            }
        }
        public DateTime? CancelDate
        {
            get { return _cancelDate; }
            set
            {
                _cancelDate = value;
                OnPropertyChanged();
            }
        }
        public DateTime? CheckInDate
        {
            get { return _checkInDate; }
            set
            {
                _checkInDate = value;
                OnPropertyChanged();
            }
        }
        public DateTime? CheckOutDate
        {
            get { return _checkOutDate; }
            set
            {
                _checkOutDate = value;
                OnPropertyChanged();
            }
        }
        public Boolean WelcomeReception
        {
            get { return _welcomeReception; }
            set
            {
                _welcomeReception = value;
                OnPropertyChanged();
            }
        }
        public Boolean Golfing
        {
            get { return _golfing; }
            set
            {
                _golfing = value;
                OnPropertyChanged();
            }
        }
        public Boolean PhotoWaiver
        {
            get { return _photoWaiver; }
        }
        public ConferenceUser Admin
        {
            get { return _admin; }
            set
            {
                _admin = value;
                OnPropertyChanged();
            }
        }
        public String RSVPNotes
        {
            get { return _rsvpNotes; }
            set
            {
                OnPropertyChanged();
            }
        }

        public Boolean IsNew
        {
            get { return _registrationDate.Equals(_objTimeStamp); }
        }

        public Boolean IsValid
        {
            get { return _validRSVP; }
        }
        public Boolean isCurrent
        {
            get { return _cancelDate.Equals(null) || _cancelDate.Equals(new DateTime(1900, 1, 1)); }
        }
        public DataTable Events
        {
            get { return _events; }
        }
        public DataTable Meals
        {
            get { return _meals; }
        }
        public DataTable Transportation
        {
            get { return _transportation; }
        }
        public DateTime Created
        {
            get { return _objTimeStamp; }
        }
       
        public RSVP(ConferenceUser user, String invitationType)
        {
            _user = user;
            _user.PropertyChanged += new PropertyChangedEventHandler(_user_PropertyChanged);
            _objTimeStamp = DateTime.Now;

            _invitationType = invitationType;
            fetchData();
            if (IsNew)
            {
                _checkInDate = Conference.Instance.CheckInStart;// Convert.ToDateTime(System.Configuration.ConfigurationManager.AppSettings["ConferenceStart"]);
                _checkOutDate = Conference.Instance.Stop; // Convert.ToDateTime(System.Configuration.ConfigurationManager.AppSettings["ConferenceStop"]);
            }

            _origRSVP = new RSVP(this);

            OnPropertyChanged();
        }


        //copy constructor for change tracking
        private RSVP(RSVP rsvp)
        {
            if (rsvp.User is KTConferenceUser)
                _user = new KTConferenceUser(rsvp.User.Email);
            else
                _user = new ExternalConferenceUser(rsvp.User.Email);
            _confirmationCode = rsvp.ConfirmationCode;
            _invitationType = rsvp.InvitationType;
            _registrationDate = rsvp.RegisistrationDate;
            _confirmDate = rsvp.ConfirmDate;
            _cancelDate = rsvp.CancelDate;
            _checkInDate = rsvp.CheckInDate;
            _checkOutDate = rsvp.CheckOutDate;
            _welcomeReception = rsvp.WelcomeReception;
            _golfing = rsvp.Golfing;
            _photoWaiver = rsvp.PhotoWaiver;
            _meals = rsvp.Meals.Copy();
            _transportation = rsvp.Transportation.Copy();
            _events = rsvp.Events.Copy();
            _objTimeStamp = rsvp.Created;

            _origMeals = GetMealsDetails().Copy();
            _origTrans = GetTransporationDetails().Copy();
        }

        private void setUser(KTConferenceUser user)
        {
        }

        private void _user_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            _user = (ConferenceUser)sender;
            OnPropertyChanged();
        }

        private void fetchData()
        {
            try
            {
                int conferenceID = getConferenceID();
                GenericCmdParameter[] parameters =  { new GenericCmdParameter("@conferenceID", conferenceID), 
                                            new GenericCmdParameter("@userID", _user.UserID) 
                                            };

                DataTable rsvp = dataUtil.webAppTable("sp_getRSVP", parameters);

                if (rsvp.Rows.Count == 1)
                {

                    DataRow row = rsvp.Rows[0];

                    _invitationType = (row["rsvpInvitationType"] == DBNull.Value ? String.Empty : Convert.ToString(row["rsvpInvitationType"]));
                    _confirmationCode = (row["confirmationCode"] == DBNull.Value ? String.Empty : Convert.ToString(row["confirmationCode"])); //can't be null -- DB restriction
                    CancelDate = (row["rsvpCancelDate"].Equals(DBNull.Value) ? new DateTime(1900, 1, 1) : Convert.ToDateTime(row["rsvpCancelDate"])); // (DateTime?)(row["rsvpCancelDate"] == DBNull.Value ? null : row["rsvpCancelDate"]);
                    _registrationDate = (!isCurrent) ? _objTimeStamp : Convert.ToDateTime(row["rsvpRegistrationDate"]); //can't be null -- DB restriction
                    ConfirmDate = (row["rsvpConfirmDate"] as DateTime?) ?? null; //(DateTime?)(row["rsvpConfirmDate"] == DBNull.Value ? null : row["rsvpConfirmDate"]);
                    CheckInDate = (row["rsvpCheckIn"] as DateTime?) ?? null; //(DateTime?)(row["rsvpCheckIn"] == DBNull.Value ? null : row["rsvpCheckIn"]);
                    CheckOutDate = (row["rsvpCheckOut"] as DateTime?) ?? null; //(DateTime?)(row["rsvpCheckOut"] == DBNull.Value ? null : row["rsvpCheckOut"]);
                    WelcomeReception = (row["rsvpWelcomeReception"] as Boolean?) ?? false;// (row["rsvpWelcomeReception"] == DBNull.Value ? false : Convert.ToBoolean(row["rsvpWelcomeReception"]));
                    Golfing = (row["rsvpGolfing"] as Boolean?) ?? false; // (row["rsvpGolfing"] == DBNull.Value ? false : Convert.ToBoolean(row["rsvpGolfing"]));
                    _photoWaiver = row["rsvpPhotoVideoWaiver"].Equals(DBNull.Value) ? false : Convert.ToBoolean(row["rsvpPhotoVideoWaiver"]);
                    string AdminEmail = Convert.ToString(row["adminEmail"]);
                    if (!AdminEmail.Equals(String.Empty))
                        _admin = new KTConferenceUser(AdminEmail);
                    RSVPNotes = Convert.ToString(row["rsvpNotes"]); //(row["rsvpNotes"] == DBNull.Value ? String.Empty : Convert.ToString(row["rsvpNotes"]));
                    _validRSVP = true;
                }
                else
                {
                    _user.FirstName = string.Empty;
                    _user.LastName = string.Empty;

                    if (_user is KTConferenceUser)
                    {
                        ((KTConferenceUser)_user).Division = string.Empty;
                    }

                    _validRSVP = false;
                    _registrationDate = _objTimeStamp;
                }
                _events = dataUtil.webAppTable("sp_GetUserRequestedEventsList", parameters);
                _events.PrimaryKey = new DataColumn[] { _events.Columns["eventID"] };

                _meals = dataUtil.webAppTable("sp_GetKTUserMealsList", parameters);
                if (_meals.Rows.Count == 0)
                    _meals = dataUtil.webAppTable("sp_GetDefaultUserMealSelections", parameters);

                foreach (DataRow row in _meals.Rows)
                {
                    if (row["lastUpdated"].Equals(DBNull.Value))
                    {
                        row.BeginEdit();
                        row["lastUpdated"] = DateTime.Now;
                        row.AcceptChanges();
                    }
                }


                _meals.PrimaryKey = new DataColumn[] { _meals.Columns["mealID"] };

                _transportation = dataUtil.webAppTable("sp_GetKTUserTransportationList", parameters);
                if (_transportation.Rows.Count == 0)
                    _transportation = dataUtil.webAppTable("sp_GetDefaultUserTransportationSelections", parameters);


                foreach (DataRow row in _transportation.Rows)
                {
                    if (row["lastUpdated"].Equals(DBNull.Value))
                    {
                        row.BeginEdit();
                        row["lastUpdated"] = DateTime.Now;
                        row.AcceptChanges();
                    }
                }


                _transportation.PrimaryKey = new DataColumn[] { _transportation.Columns["transportationDirection"] };
                System.Web.HttpContext.Current.Session["rsvp"] = this;
                OnPropertyChanged();
            }
            catch (Exception e)
            {
                _validRSVP = false;
            }

        }

        public void setPhotoWaiver()
        {
            _photoWaiver = true;
            OnPropertyChanged();
        }

        private string generateConfirmationCode()
        {
            string confirmationCode = string.Empty;
            int employeeID = 0;
            string seed = DateTime.Now.Ticks.ToString();
            seed = seed.Substring(seed.Length - 8, 8);
            if (this.User is KTConferenceUser)
            {
                Int32.TryParse(Regex.Replace(((KTConferenceUser)this.User).EmployeeID, "[^0-9.]", ""), out employeeID);
            }

            confirmationCode = Common.CreateConfirmationNumber(Convert.ToInt32(seed) + employeeID);
            confirmationCode = confirmationCode.Length > 8 ? confirmationCode.Substring(0, 8) : confirmationCode;
            //check here to make sure confirmationCode doesn't exist.

            object isCodeUnique = null;

            WebDataUtility.Instance.webAppScalar("sp_IsUniqueConfirmationCode", new GenericCmdParameter[] { 
                            new GenericCmdParameter("@confirmationCode", confirmationCode) }, ref isCodeUnique);

            while (!Convert.ToBoolean(isCodeUnique))
            {
                //db check if exists

                if (Convert.ToBoolean(isCodeUnique))
                    break;
                else
                {
                    int newStarterSeed = 0;
                    Guid newGuid = Guid.NewGuid();
                    if (!newGuid.Equals(Guid.Empty))
                    {
                        string newSeed = Regex.Replace(Guid.NewGuid().ToString("N"), "[^0-9.]", "");
                        if (Int32.TryParse(newSeed.Substring(0, 4), out newStarterSeed))
                        {
                            confirmationCode = Common.CreateConfirmationNumber(newStarterSeed);
                            confirmationCode = confirmationCode.Length > 8 ? confirmationCode.Substring(0, 8) : confirmationCode;
                            //check code again.
                            WebDataUtility.Instance.webAppScalar("sp_IsUniqueConfirmationCode", new GenericCmdParameter[] { 
                                            new GenericCmdParameter("@confirmationCode", confirmationCode) }, ref isCodeUnique);

                            if (Convert.ToBoolean(isCodeUnique))
                                break;
                        }
                    }
                }
            }


            return confirmationCode;

        }

        public bool ContainsEvent(int eventID)
        {
            try
            {
                DataRow row = _events.Rows.Find(eventID);
                if (row != null)
                {
                    return true;
                }
                else
                    return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private void updateConfirmationCode()
        {
            object retval = null;

            dataUtil.webAppCmd("sp_UpdateConfirmationCode", new GenericCmdParameter[] { new GenericCmdParameter("@userID", _user.UserID), 
                                                                                        new GenericCmdParameter("@conferenceID", Conference.Instance.ID),
                                                                                        new GenericCmdParameter("@confirmationCode", _confirmationCode)}, ref retval);
        }

        public void UpdateConfirmationFlag()
        {
     
            object retval = null;

            dataUtil.webAppCmd("sp_UpdateRSVPConfirmationFlag", new GenericCmdParameter[] { new GenericCmdParameter("@userID", _user.UserID), 
                                                                                            new GenericCmdParameter("@confirmation", _confirmationCode)}, ref retval);        
        }
        
        private bool uploadRSVP()
        {
            object retval = null;
            if (IsNew || !isCurrent)
            {
                _registrationDate = DateTime.Now;
                _confirmationCode = generateConfirmationCode();
            }
            _cancelDate = new DateTime(1900, 1, 1);

            User.Update();
            dataUtil.webAppCmd("sp_LoadRSVP", new GenericCmdParameter[] { new GenericCmdParameter("@rsvp", ToTable()) }, ref retval);
            return isSuccess(retval);
        }

        private bool uploadEvents()
        {
            object retval = null;

            dataUtil.webAppCmd("sp_LoadEventAssignments", new GenericCmdParameter[] { new GenericCmdParameter("@selections", _events) }, ref retval);
            return isSuccess(retval);
        }

        private bool uploadMeals()
        {
            object retval = null;

            dataUtil.webAppCmd("sp_LoadUserMealSelections", new GenericCmdParameter[] { new GenericCmdParameter("@selections", _meals) }, ref retval);
            return isSuccess(retval);
        }

        private bool uploadTransportation()
        {
            object retval = null;

            dataUtil.webAppCmd("sp_LoadUserTransportationSelections", new GenericCmdParameter[] { new GenericCmdParameter("@selections", _transportation) }, ref retval);
            return isSuccess(retval);
        }

        private bool uploadAdmin()
        {
            if (_admin != null)
            {
                return _admin.Update();
            }
            else
                return true;
        }

        public bool CancelRSVP(string cancelReason)
        {
            object retval = null;

            dataUtil.webAppCmd("sp_DeleteUserRSVP", new GenericCmdParameter[] { 
                new GenericCmdParameter("@userID", User.UserID), 
                new GenericCmdParameter("@conferenceID", Conference.Instance.ID),
                new GenericCmdParameter("@cancelReason", cancelReason)}, ref retval);
            bool success = isSuccess(retval);

            if (success)
            {
                _cancelDate = DateTime.Now;
                _meals.Clear();
                _transportation.Clear();
                _events.Clear();
                SendEmailCancellation();
                _confirmationCode = string.Empty;
            }
            return success;
        }

        private void initializeSelectionTables()
        {
            //could have assigned events even if no RSVP (i.e. Panel Moderators, Speakers, etc.)
            if (_events.Rows.Count == 0)
            {
                _events.Columns.Add("userID", typeof(int));
                _events.Columns.Add("eventID", typeof(int));
                _events.Columns.Add("parentEventID", typeof(int));
                _events.Columns.Add("userRole", typeof(string));
                _events.Columns.Add("eventRequestOrder", typeof(int));
                _events.Columns.Add("eventAssigned", typeof(bool));
                _events.Columns.Add("lastUpdated", typeof(DateTime));
                DataColumn[] eventKeys = { _events.Columns["userID"], _events.Columns["eventID"] };
                _events.PrimaryKey = eventKeys;
            }

            //if _isNew, then no Meal or Transporation Data yet, safe to create new tables for these.
            _meals.Columns.Add("userID", typeof(int));
            _meals.Columns.Add("conferenceID", typeof(int));
            _meals.Columns.Add("mealID", typeof(int));
            _meals.Columns.Add("mealOptionID", typeof(int));
            _meals.Columns.Add("lastUpdated", typeof(DateTime));
            DataColumn[] mealKeys = { _meals.Columns["userID"], _meals.Columns["mealID"] };
            _meals.PrimaryKey = mealKeys;

            //uses direction as a primary key to ensure only one choice per direction is allowed.
            _transportation.Columns.Add("userID", typeof(int));
            _transportation.Columns.Add("conferenceID", typeof(int));
            _transportation.Columns.Add("userTransportationOptionID", typeof(int));
            _transportation.Columns.Add("userTransporationDirection", typeof(string));
            _transportation.Columns.Add("lastUpdated", typeof(DateTime));
            DataColumn[] transportationtKeys = { _transportation.Columns["userID"], 
                                                 _transportation.Columns["userTransporationDirection"] };
            _transportation.PrimaryKey = transportationtKeys;
        }

        private int getConferenceID()
        {
            return Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["ConferenceID"]);
        }
        
        public void SendEmailCancellation()
        {
            string body = _user.FirstName + ",<p>Your registration to the 2015 Engineering Conference was cancelled on "
                + ((DateTime)this.CancelDate).ToString("dddd, MMM d, yyyy h:mm tt") +
                ".</p>If you wish to re-register, you must do so by the close of the registration window.<p>" + _confirmationCode + "</p>";

            string subject = "Engineering Conference Registration Cancellation";

            Email email = null;

            string adminEmail = (_admin == null ? string.Empty : _admin.Email);

            if (adminEmail.Equals(String.Empty))
                email = new Email(User.Email, body, subject);
            else
            {
                email = new Email(User.Email, body, subject);
            }
            email = new Email(User.Email, body, subject);
            email.AddBccAddressee(Conference.Instance.POCs);
            email.Send();
        }

        public void SendEmailConfirmation(string htmlBody, bool IsNewRegistration)
        {
            string body = htmlBody;
            string subject = (IsNewRegistration ? "New " : String.Empty) + "Registration " + (IsNewRegistration ? String.Empty : "Update ") + "Confirmation";

            Email email = null;

            string adminEmail = (_admin == null ? string.Empty : _admin.Email);

            if (adminEmail.Equals(String.Empty))
                email = new Email(User.Email, htmlBody, subject);
            else
            {
                email = new Email(User.Email, adminEmail, htmlBody, subject);
            }

            if (IsNewRegistration)
                email.AddBccAddressee(Conference.Instance.POCs);

            email.AddAttachmentFromString(CreateCalendarObject(), "EngConference" + _conferenceYear +  ".ics");
            email.Send();
        }

        public void SendEmailConfirmation(AlternateView view, string htmlBody, bool IsNewRegistration)
        {
            ExceptionUtility.LogException(new Exception(), "INSIDE SendEmailConfirmation");
            Email email = null;

            string subject = (IsNewRegistration ? "New " : String.Empty) + "Registration " + (IsNewRegistration ? String.Empty : "Update ") + "Confirmation";
            string adminEmail = (_admin == null ? string.Empty : _admin.Email);
            
            email = new Email(System.Configuration.ConfigurationManager.AppSettings["sysadmin"] , htmlBody, subject);

            if (IsNewRegistration)
                email.AddBccAddressee(Conference.Instance.POCs);

            email.AddAttachmentFromString(CreateCalendarObject(), "EngConference" + _conferenceYear + ".ics");
            email.Send();
        }

        public void SendChangeEmail()
        {
            string body = getChangeEmailBody(analyzeChanges());
            if (body.Length == 0)
                return;

            string subject = "KT Engineering Conference " + _conferenceYear + " - Registration Update";
            Email email = new Email(Conference.Instance.POCs, body, subject);
            email.Send();
        }

        private iCalendar createiCalObject()
        {
            iCalendar iCal = new iCalendar();
            iCal.Method = "PUBLISH";

            Event evt = iCal.Create<Event>();
            evt.Summary = "Engineering Conference " + _conferenceYear + " - Accelerate Innovation";
            DateTime confStart = Convert.ToDateTime(System.Configuration.ConfigurationManager.AppSettings["ConferenceStart"]);
            DateTime confStop = Convert.ToDateTime(System.Configuration.ConfigurationManager.AppSettings["ConferenceStop"]);
            DateTime checkin = ((DateTime)_checkInDate).AddHours(15);
            evt.Start = (_checkInDate < confStart ? new iCalDateTime(checkin) : new iCalDateTime(confStart));
            evt.End = (_checkOutDate > confStop ? new iCalDateTime((DateTime)_checkOutDate) : new iCalDateTime(confStop));
            //            evt.Duration = new TimeSpan((int)Math.Ceiling((confStop - confStart).TotalHours), 0, 0, 0);
            evt.Description = "Engineering Conference " + _conferenceYear;
            evt.Location = "Hyatt Regency Hotel, 1 Old Golf Course Rd, Monterey, CA 93940";
            evt.IsAllDay = false;
            evt.Organizer = new Organizer("KLA-Tencor Engineering Conference <EngineeringConference@kla-tencor.com>");
            evt.UID = System.Configuration.ConfigurationManager.AppSettings["ConferenceGUID"];
            iCal.Method = "REQUEST";
            return iCal;
        }

        private string CreateCalendarObject()
        {
            iCalendarSerializer serializer = new iCalendarSerializer(createiCalObject());
            return serializer.SerializeToString();
        }

        public string CreateCalendarObjectForDownload()
        {
            iCalendar iCal = createiCalObject();
            iCalendarSerializer serializer = new iCalendarSerializer();
            string fileName = this.User.FirstName + "." + this.User.LastName + ".EngConference" + _conferenceYear +  ".ics";
            string path = HttpContext.Current.Request.PhysicalApplicationPath + @"\Temp\"
                + fileName;
            File.Delete(path);
            serializer.Serialize(iCal, path);
            return fileName;
        }

        public void ClearEvents()
        {
            //CANT DO _events.Clear()!!! CANNOT REMOVE ITEMS WHERE eventAssignend == TRUE (i.e. Speakers, Tech Panel guys, etc.)
            DataRow[] rows = _events.Select("eventAssigned = false");
            if (rows.Length > 0)
            {
                foreach (DataRow r in rows)
                    _events.Rows.Remove(r);
            }
            OnPropertyChanged();

        }
        public void ClearEvent(int eventID)
        {
            DataRow[] rows = _events.Select("eventID = " + eventID + " AND eventAssigned=false");
            if (rows.Length > 0)
                foreach (DataRow r in rows)
                    _events.Rows.Remove(r);
            OnPropertyChanged();

        }

        public void ClearMeals()
        {
            _meals.Clear();
            OnPropertyChanged();

        }
        
        public void ClearTransportation()
        {
            _transportation.Clear();
            OnPropertyChanged();

        }

        public void SetEventItems(List<EventItem> selections)
        {
            //NEED TO FIGURE A WAY NOT TO DELETE OR OVERWRITE EVENTS WHERE eventAssigned = TRUE (i.e. Speakers, Tech Panel guys, etc.)
            foreach (EventItem item in selections)
            {
                DataRow[] rows = _events.Select("parentEventID = " + item.ParentEventID + " AND eventAssigned = true");
                if (rows.Length == 0)
                {
                    foreach (DataRow row in _events.Select("parentEventID = " + item.ParentEventID +
                                    " AND (eventRequestOrder = " + item.RequestOrder + " OR eventID = " + item.EventID + ") AND eventAssigned = false"))
                    {
                        _events.Rows.Remove(row);
                    }
                    DataRow newRow = _events.NewRow();
                    newRow["userID"] = _user.UserID;
                    newRow["eventID"] = item.EventID;
                    newRow["parentEventID"] = item.ParentEventID;
                    newRow["userRole"] = item.UserRole;
                    newRow["eventRequestOrder"] = item.RequestOrder;
                    newRow["eventAssigned"] = false;
                    newRow["lastUpdated"] = DateTime.Now;

                    _events.Rows.Add(newRow);
                }
                OnPropertyChanged();

            }
        }

        public void AssignEvent(int parentEventID, int childEventID)
        {
            foreach (DataRow row in _events.Rows)
            {
                if (DBNullable.ToInt(row["parentEventID"]) != parentEventID)
                    continue;
                row["eventAssigned"] = DBNullable.ToInt(row["eventID"]) == childEventID;
            }
        }

        public void SetMealItems(List<MealItem> selections)
        {

            foreach (MealItem item in selections)
            {
                DataRow meal = _meals.Rows.Find(item.MealID);
                if (meal != null)
                {
                        meal["mealOptionID"] = item.MealOptionID;
                        meal["lastUpdated"] = DateTime.Now;
                }
                else
                {
                        DataRow newRow = _meals.NewRow();
                        newRow["userID"] = _user.UserID;
                        newRow["conferenceID"] = Conference.Instance.ID;
                        newRow["mealID"] = item.MealID;
                        newRow["mealOptionID"] = item.MealOptionID;
                        newRow["lastUpdated"] = DateTime.Now;
                        _meals.Rows.Add(newRow);
                }
            }
            _meals.AcceptChanges();
            OnPropertyChanged();

        }

        public void ClearMealChoice(int mealID)
        {
            DataRow mealToClear = _meals.Rows.Find(mealID);
            if (mealToClear != null)
                _meals.Rows.Remove(mealToClear);
            _meals.AcceptChanges();
            OnPropertyChanged();

        }

        public void SetTransporationItem(List<TransportationItem> selections)
        {
            foreach (TransportationItem item in selections)
            {
                DataRow[] existing = _transportation.Select("transportationDirection = '" + item.TransporationDirection + "'");

                if (existing.Length > 0)
                {
                    if (Convert.ToInt32(existing[0]["userTransportationOptionID"]) != item.TransporationModeID)
                    {
                        _transportation.Rows.Remove(existing[0]);
                    }
                    else
                        return;
                }

                DataRow newRow = _transportation.NewRow();
                newRow["userID"] = _user.UserID;
                newRow["conferenceID"] = Conference.Instance.ID;
                newRow["userTransportationOptionID"] = item.TransporationModeID;
                newRow["transportationDirection"] = item.TransporationDirection;
                newRow["lastUpdated"] = DateTime.Now;
                _transportation.Rows.Add(newRow);
            }
            _transportation.AcceptChanges();
            OnPropertyChanged();

        }

        public DataTable GetTechPanelsDetails()
        {
            return dataUtil.webAppTable("sp_GetUserRequestedTechPanelsList", new GenericCmdParameter[]
            {
                new GenericCmdParameter("@conferenceID", getConferenceID()),
                new GenericCmdParameter("@userID", User.UserID)

            });

        }

        public DataTable GetPaperDetails()
        {
            return dataUtil.webAppTable("sp_GetUserRequestedPapersList", new GenericCmdParameter[]
            {
                new GenericCmdParameter("@conferenceID", getConferenceID()),
                new GenericCmdParameter("@userID", User.UserID)

            });
        }

        public DataTable GetMealsDetails()
        {
            DataTable mealDatails = dataUtil.webAppTable("sp_GetKTUserMealsListView", new GenericCmdParameter[]
            {
                new GenericCmdParameter("@conferenceID", getConferenceID()),
                new GenericCmdParameter("@userID", User.UserID)

            });
            DataColumn pk = mealDatails.Columns["mealID"];
            mealDatails.PrimaryKey = new DataColumn[] { mealDatails.Columns["mealID"] };
           
            foreach (DataRow row in mealDatails.Rows)
            {
                if (row["lastUpdated"].Equals(DBNull.Value))
                {
                    row.BeginEdit();
                    row["lastUpdated"] = DateTime.Now;
                    row.AcceptChanges();
                }
            }
            return mealDatails;
        }

        public DataTable GetTransporationDetails()
        {
            DataTable transDetails = dataUtil.webAppTable("sp_GetKTUserTransportationListView", new GenericCmdParameter[]
            {
                new GenericCmdParameter("@conferenceID", getConferenceID()),
                new GenericCmdParameter("@userID", User.UserID)

            });
            transDetails.PrimaryKey = new DataColumn[] { transDetails.Columns["transportationDirection"] };

            foreach (DataRow row in transDetails.Rows)
            {
                if (row["lastUpdated"].Equals(DBNull.Value))
                {
                    row.BeginEdit();
                    row["lastUpdated"] = DateTime.Now;
                    row.AcceptChanges();
                }
            }
            return transDetails;
        }

        public bool Update()
        {
            bool retval = false;
            retval = uploadRSVP();
            retval = retval && uploadEvents();
            retval = retval && uploadMeals();
            retval = retval && uploadTransportation();
            retval = retval && uploadAdmin();

            _validRSVP = retval;

            //if (!IsNew && !isCurrent)
            //    SendChangeEmail();

            return retval;

        }

        public DataTable ToTable()
        {
            DataTable retval = new DataTable("rsvp");

            DateTime minDate = new DateTime(1900, 1, 1);

            retval.Columns.Add("userID", typeof(int));
            retval.Columns.Add("conferenceID", typeof(int));
            retval.Columns.Add("confirmationCode", typeof(string));
            retval.Columns.Add("rsvpInvitationType", typeof(string));
            retval.Columns.Add("rsvpRegistrationDate", typeof(DateTime));
            retval.Columns.Add("rsvpConfirmDate", typeof(DateTime));
            retval.Columns.Add("rsvpCheckIn", typeof(DateTime));
            retval.Columns.Add("rsvpCheckOut", typeof(DateTime));
            retval.Columns.Add("rsvpCancelDate", typeof(DateTime));
            retval.Columns.Add("rsvpWelcomeReception", typeof(bool));
            retval.Columns.Add("rsvpGolfing", typeof(bool));
            retval.Columns.Add("adminEmail", typeof(string));
            retval.Columns.Add("rsvpNotes", typeof(string));
            retval.Columns.Add("rsvpPhotoVideoWaiver", typeof(string));
            retval.Columns.Add("lastUpdated", typeof(DateTime));

            DataRow newRow = retval.NewRow();
            newRow["userID"] = this._user.UserID;
            newRow["conferenceID"] = Conference.Instance.ID;
            newRow["confirmationCode"] = this._confirmationCode;
            newRow["rsvpInvitationType"] = this._invitationType;
            newRow["rsvpRegistrationDate"] = this._registrationDate;
            newRow["rsvpConfirmDate"] = this._confirmDate == null ? minDate : this._confirmDate;
            newRow["rsvpCheckIn"] = this._checkInDate == null ? minDate : this._checkInDate;
            newRow["rsvpCheckOut"] = this._checkOutDate == null ? minDate : this._checkOutDate;
            newRow["rsvpCancelDate"] = this._cancelDate == null ? minDate : this._cancelDate;
            newRow["rsvpWelcomeReception"] = this._welcomeReception;
            newRow["rsvpGolfing"] = this._golfing;
            newRow["adminEmail"] = (_admin == null ? string.Empty : this._admin.Email);
            newRow["rsvpNotes"] = this._rsvpNotes;
            newRow["rsvpPhotoVideoWaiver"] = this._photoWaiver;
            newRow["lastUpdated"] = DateTime.Now;
            retval.Rows.Add(newRow);
            return retval;
        }

        //public void PrintSchedule();
        //public HttpCookie ToCookie();
        //public void SendEmail()
        //{
        //    return;
        //}

        private bool isSuccess(object retval)
        {
            if (retval == null)
                return false;
            else
                return (Convert.ToInt32(retval) == 0);
        }
        private DataTable _origMeals = new DataTable("origMeals");
        private DataTable _origTrans = new DataTable("origTrans");

        public DataTable getOrigMealDetails()
        {
            return _origMeals;
        }

        public DataTable getOrigTransDetails()
        {
            return _origTrans;
        }
        private DataTable analyzeChanges()
        {
            if (_origRSVP == null)
                return null;

            DataTable changes = new DataTable("changes");

            changes.Columns.Add(new DataColumn("item", typeof(string)));
            changes.Columns.Add(new DataColumn("origValue", typeof(object)));
            changes.Columns.Add(new DataColumn("finalValue", typeof(object)));

            if (!(_origRSVP.User.FirstName.Equals(this.User.FirstName)) || !(_origRSVP.User.LastName.Equals(this.User.LastName)))
            {
                DataRow newRow = changes.NewRow();
                newRow["item"] = "Name";
                newRow["origValue"] = _origRSVP.User.FullName;
                newRow["finalValue"] = this.User.FullName;
                changes.Rows.Add(newRow);
            }

            if (!(_origRSVP.CheckInDate.Equals(this.CheckInDate)))
            {
                DataRow newRow = changes.NewRow();
                newRow["item"] = "Check In Date";
                newRow["origValue"] = _origRSVP.CheckInDate;
                newRow["finalValue"] = this.CheckInDate;
                changes.Rows.Add(newRow);
            }

            if (!(_origRSVP.CheckOutDate.Equals(this.CheckOutDate)))
            {
                DataRow newRow = changes.NewRow();
                newRow["item"] = "Check Out Date";
                newRow["origValue"] = _origRSVP.CheckOutDate;
                newRow["finalValue"] = this.CheckOutDate;
                changes.Rows.Add(newRow);
            }

            if (!(_origRSVP.WelcomeReception.Equals(this.WelcomeReception)))
            {
                DataRow newRow = changes.NewRow();
                newRow["item"] = "Welcome Reception";
                newRow["origValue"] = _origRSVP.WelcomeReception ? "Attending" : "Not Attending";
                newRow["finalValue"] = this.WelcomeReception ? "Attending" : "Not Attending";
                changes.Rows.Add(newRow);
            }

            if (!(_origRSVP.Golfing.Equals(this.Golfing)))
            {
                DataRow newRow = changes.NewRow();
                newRow["item"] = "Golf";
                newRow["origValue"] = _origRSVP.Golfing ? "Yes" : "No";
                newRow["finalValue"] = this.Golfing ? "Yes" : "No";
                changes.Rows.Add(newRow);
            }

            if (!(_origRSVP.User.MobilePhone.Equals(this.User.MobilePhone)))
            {
                DataRow newRow = changes.NewRow();
                newRow["item"] = "MobilePhone";
                newRow["origValue"] = _origRSVP.User.MobilePhone;
                newRow["finalValue"] = this.User.MobilePhone;
                changes.Rows.Add(newRow);
            }
            if (_origRSVP.User is KTConferenceUser)
            {
                if (!((((KTConferenceUser)_origRSVP.User).Division.Equals(((KTConferenceUser)this.User).Division))))
                {
                    DataRow newRow = changes.NewRow();
                    newRow["item"] = "Division";
                    newRow["origValue"] = ((KTConferenceUser)_origRSVP.User).Division;
                    newRow["finalValue"] = ((KTConferenceUser)this.User).Division;
                    changes.Rows.Add(newRow);
                }

                if (!((((KTConferenceUser)_origRSVP.User).JobRole.Equals(((KTConferenceUser)this.User).JobRole))))
                {
                    DataRow newRow = changes.NewRow();
                    newRow["item"] = "JobRole";
                    newRow["origValue"] = ((KTConferenceUser)_origRSVP.User).JobRole;
                    newRow["finalValue"] = ((KTConferenceUser)this.User).JobRole;
                    changes.Rows.Add(newRow);
                }

                if (!((((KTConferenceUser)_origRSVP.User).HomeOffice.Equals(((KTConferenceUser)this.User).HomeOffice))))
                {
                    DataRow newRow = changes.NewRow();
                    newRow["item"] = "HomeOffice";
                    newRow["origValue"] = ((KTConferenceUser)_origRSVP.User).HomeOffice;
                    newRow["finalValue"] = ((KTConferenceUser)this.User).HomeOffice;
                    changes.Rows.Add(newRow);
                }


                if (!((((KTConferenceUser)_origRSVP.User).ShirtSize.Equals(((KTConferenceUser)this.User).ShirtSize))))
                {
                    DataRow newRow = changes.NewRow();
                    newRow["item"] = "ShirtSize";
                    newRow["origValue"] = ((KTConferenceUser)_origRSVP.User).ShirtSize;
                    newRow["finalValue"] = ((KTConferenceUser)this.User).ShirtSize;
                    changes.Rows.Add(newRow);
                }

            }

            //find rows in _orig NOT IN final --deleted meails
            foreach (DataRow mealRow in _origRSVP.getOrigMealDetails().Rows)
            {
                DataRow row = this.GetMealsDetails().Rows.Find(mealRow["mealID"]);
                if (row == null) // meal was deleted
                {
                    DataRow newRow = changes.NewRow();
                    newRow["item"] = "Meal Change";
                    newRow["origValue"] = Convert.ToDateTime(mealRow["mealDate"]).ToShortDateString() + " - " + mealRow["mealType"] + " selected.";
                    newRow["finalValue"] = Convert.ToDateTime(mealRow["mealDate"]).ToShortDateString() + " - " + mealRow["mealType"] + " removed.";
                    changes.Rows.Add(newRow);
                }
                else // check if meal changed
                {
                    if (!mealRow["mealOptionName"].Equals(row["mealOptionName"])) //meal option changed
                    {
                        DataRow newRow = changes.NewRow();
                        newRow["item"] = "Meal Change";
                        newRow["origValue"] = Convert.ToDateTime(mealRow["mealDate"]).ToShortDateString() + " - " + mealRow["mealType"] + " choice: " + mealRow["mealOptionName"];
                        newRow["finalValue"] = Convert.ToDateTime(mealRow["mealDate"]).ToShortDateString() + " - " + row["mealType"] + " choice updated to: " + row["mealOptionName"];
                        changes.Rows.Add(newRow);
                    }
                }
            }
            //find rows in final NOT IN _orig --added meals
            foreach (DataRow mealRow in this.GetMealsDetails().Rows)
            {
                DataRow row = _origRSVP.getOrigMealDetails().Rows.Find(mealRow["mealID"]);
                if (row == null) // meal was added
                {
                    DataRow newRow = changes.NewRow();
                    newRow["item"] = "Meal Change";
                    newRow["origValue"] = mealRow["mealType"] + " not selected.";
                    newRow["finalValue"] = mealRow["mealType"] + " added.";
                    changes.Rows.Add(newRow);
                }

            }

            if (!(_origRSVP.User.FoodAllergies.Equals(this.User.FoodAllergies)))
            {
                DataRow newRow = changes.NewRow();
                newRow["item"] = "FoodAllergies";
                newRow["origValue"] = _origRSVP.User.FoodAllergies;
                newRow["finalValue"] = this.User.FoodAllergies;
                changes.Rows.Add(newRow);
            }
            if (!(_origRSVP.User.SpecialNeeds.Equals(this.User.SpecialNeeds)))
            {
                DataRow newRow = changes.NewRow();
                newRow["item"] = "Special Needs";
                newRow["origValue"] = _origRSVP.User.SpecialNeeds;
                newRow["finalValue"] = this.User.SpecialNeeds;
                changes.Rows.Add(newRow);
            }

            foreach (DataRow transRow in _origRSVP.getOrigTransDetails().Rows)
            {
                DataRow row = this.GetTransporationDetails().Rows.Find(transRow["transportationDirection"]);
                if (row == null)
                {
                    DataRow newRow = changes.NewRow();
                    newRow["item"] = "Transportation";
                    newRow["origValue"] = transRow["transportationDirection"] + " - " + transRow["transportationModeText"] + " @ "
                                        + Convert.ToDateTime(transRow["transportationDepartTime"]).ToShortDateString() + " "
                                        + Convert.ToDateTime(transRow["transportationDepartTime"]).ToShortTimeString();
                    newRow["finalValue"] = "Nothing";
                    changes.Rows.Add(newRow);
                }
                else
                {
                    if (!transRow["userTransportationOptionID"].Equals(row["userTransportationOptionID"]))
                    {
                        DataRow newRow = changes.NewRow();
                        newRow["item"] = "Transportation";
                        newRow["origValue"] = transRow["transportationDirection"] + " - " + transRow["transportationModeText"] + " @ "
                                            + Convert.ToDateTime(transRow["transportationDepartTime"]).ToShortDateString() + " "
                                            + Convert.ToDateTime(transRow["transportationDepartTime"]).ToShortTimeString();
                        newRow["finalValue"] = row["transportationDirection"] + " - " + row["transportationmodeText"] + " @ "
                                            + Convert.ToDateTime(row["transportationDepartTime"]).ToShortDateString() + " "
                                            + Convert.ToDateTime(row["transportationDepartTime"]).ToShortTimeString();
                        changes.Rows.Add(newRow);
                    }
                }
            }


            return changes;
        }

        private string getChangeEmailBody(DataTable data)
        {
            if (data.Rows.Count == 0)
                return string.Empty;

            StringBuilder builder = new StringBuilder("<p>Changes made to <b><a href='mailto:" + this.User.Email + "'>" + this.User.FullName + "'s</a></b> registration data made on : "
                + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + "</p>");
            builder.AppendLine("<p><b>Confirmation Code:</b>&nbsp;" + ConfirmationCode + "</p>");
            builder.AppendLine("<table style='border-collapse:collapse;'>");
            builder.AppendLine("<td valign='top' style='font-weight:bold; width:150px;border:1px solid black'>Data Item</td>");
            builder.AppendLine("<td valign='top' style='font-weight:bold; width:300px;border:1px solid black'>Original Value</td>");
            builder.AppendLine("<td valign='top' style='font-weight:bold; width:300px;border:1px solid black'>Updated Value</td>");
            foreach (DataRow row in data.Rows)
            {
                builder.AppendLine("<tr>");

                builder.AppendLine("<td valign='top' style='border:1px solid black;'>" + row["item"] + "</td>");
                builder.AppendLine("<td valign='top' style='border:1px solid black;'>" + row["origValue"] + "</td>");
                builder.AppendLine("<td valign='top' style='border:1px solid black;'>" + row["finalValue"] + "</td>");

                builder.AppendLine("</tr>");

            }

            builder.AppendLine("</table>");
            builder.AppendLine("<p>End of changes</p>");
            return builder.ToString();
        }
    }

    public abstract class ConferenceUser : INotifyPropertyChanged
    {

        protected class SpeakerAttributes
        {
            private string userPrefix = string.Empty;
            private string userTitle = string.Empty;
            private string userHTMLBio = string.Empty;
            private string userImageHyperlink = string.Empty;

            private bool _hasData = false;
            public bool HasData
            {
                get { return _hasData; }
            }

            public string Prefix
            {
                get { return userPrefix; }
                set { userPrefix = value; }
            }

            public string Title
            {
                get { return userTitle; }
                set { userTitle = value; }
            }

            public string Bio
            {
                get { return userHTMLBio; }
                set { userHTMLBio = value; }
            }

            public string ImageHyperLink
            {
                get { return userImageHyperlink; }
                set { userImageHyperlink = value; }
            }

            public SpeakerAttributes(MemberAttributesCollection attributes)
            {
                loadAttributes(attributes);
            }

            public SpeakerAttributes(string email)
            {

                DataTable table = WebDataUtility.Instance.webAppTable("sp_GetSpeakerAddOnInfo",
                    new GenericCmdParameter[] { new GenericCmdParameter("@conferenceID", Conference.Instance.ID),
                                            new GenericCmdParameter("@userEmail", email) });

                if (table.Rows.Count == 1)
                    _hasData = true;
                else
                    return;

                MemberAttributesCollection attribs = new MemberAttributesCollection();

                foreach (DataColumn col in table.Columns)
                    attribs.Add(new MemberAttribute(col.ColumnName, table.Rows[0][col.ColumnName]));

                loadAttributes(attribs);
            }

            private void loadAttributes(MemberAttributesCollection attributes)
            {
                foreach (MemberAttribute attr in attributes)
                {
                    switch (attr.Name)
                    {
                        case ("userPrefix"):
                            {
                                userPrefix = DBNullable.ToString(attr.Value);
                                break;
                            }
                        case ("userTitle"):
                            {
                                userTitle = DBNullable.ToString(attr.Value);
                                break;
                            }
                        case ("userHTMLBio"):
                            {
                                userHTMLBio = DBNullable.ToString(attr.Value);
                                break;
                            }
                        case ("userImageHyperlink"):
                            {
                                userImageHyperlink = DBNullable.ToString(attr.Value);
                                break;
                            }
                    }
                }
            }

            public void Upload()
            {
                //TO DO UPLOAD SPEAKER ADD-ON DATA - NEED NEW TABLE TYPE AND SP

            }

            private DataTable ToTable()
            {
                DataTable retval = new DataTable("userAddOn");

                retval.Columns.Add("userPrefix", typeof(string));
                retval.Columns.Add("userTitle", typeof(string));
                retval.Columns.Add("userHTMLBio", typeof(string));
                retval.Columns.Add("userImageHyperlink", typeof(string));
                retval.Columns.Add("lastUpdated", typeof(DateTime));

                DataRow newRow = retval.NewRow();
                newRow["userPrefix"] = this.userPrefix;
                newRow["userTitle"] = this.userTitle;
                newRow["userHTMLBio"] = this.userHTMLBio;
                newRow["userImageHyperlink"] = this.userImageHyperlink;
                newRow["lastUpdated"] = DateTime.Now;
                retval.Rows.Add(newRow);
                return retval;
            }
        }

        protected int userID = -1;
        protected string firstName;
        protected string lastName;
        protected string email;
        protected string workPhone = string.Empty;
        protected string mobilePhone = string.Empty;
        protected string foodAllergies = string.Empty;
        protected string specialNeeds = string.Empty;
        protected bool isInvitee = false;
        
        protected WebDataUtility dataUtil = WebDataUtility.Instance;
        protected bool isRegistered;

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public int UserID
        {
            get { return userID; }
        }
        public String FullName
        {
            get { return firstName + " " + lastName; }
        }
        public String FirstName
        {
            get { return firstName; }
            set
            {
                firstName = value;
                OnPropertyChanged();
            }
        }
        public String LastName
        {
            get { return lastName; }
            set { lastName = value; OnPropertyChanged(); }
        }
        public String WorkPhone
        {
            get { return workPhone; }
            set { workPhone = value; OnPropertyChanged(); }
        }
        public String MobilePhone
        {
            get { return mobilePhone; }
            set { mobilePhone = value; OnPropertyChanged(); }
        }
        public String Email
        {
            get { return email; }
        }
        public String FoodAllergies
        {
            get { return foodAllergies; }
            set { foodAllergies = value; OnPropertyChanged(); }
        }
        public String SpecialNeeds
        {
            get { return specialNeeds; }
            set { specialNeeds = value; OnPropertyChanged(); }
        }

        public bool IsInvitee
        {
            //Invitee Definition

            //Currently Registered as guest OR
            //Primary AND datetime within primary registration date
            //secondary AND datetime.now within alternate registration date
            //tertiary and datetime.now < conference start date

            get { return isInvitee; }
        }
        public Boolean isSpeaker
        {
            get { return isPresenter(); }
        }

        public bool IsExec()
        {
            object retval = null;
            
            WebDataUtility.Instance.webAppScalar("sp_IsExec",
                new GenericCmdParameter[] { new GenericCmdParameter("@userID", this.userID), new GenericCmdParameter("@conferenceID", Conference.Instance.ID) }, ref retval);

            return DBNullable.ToBool(retval);
        }

        //protected methods
        protected DataTable UserTable()
        {
            DataTable retval = new DataTable("user");

            retval.Columns.Add("userEmail", typeof(string));
            retval.Columns.Add("userFirstName", typeof(string));
            retval.Columns.Add("userLastName", typeof(string));
            retval.Columns.Add("userWorkPhone", typeof(string));
            retval.Columns.Add("userMobilePhone", typeof(string));
            retval.Columns.Add("userFoodAllergies", typeof(string));
            retval.Columns.Add("userSpecialNeeds", typeof(string));
            retval.Columns.Add("lastUpdated", typeof(DateTime));

            DataRow newRow = retval.NewRow();
            newRow["userEmail"] = this.email;
            newRow["userFirstName"] = this.firstName;
            newRow["userLastName"] = this.lastName;
            newRow["userWorkPhone"] = this.workPhone;
            newRow["userMobilePhone"] = this.mobilePhone;
            newRow["userFoodAllergies"] = this.foodAllergies.Trim();
            newRow["userSpecialNeeds"] = this.specialNeeds.Trim();
            newRow["lastUpdated"] = DateTime.Now;
            retval.Rows.Add(newRow);
            return retval;

        }

        public bool isAdminForKTUser(string otherEmail, string inviteType)
        {
            RSVP rsvp = new RSVP(new KTConferenceUser(otherEmail), inviteType);
            if (!rsvp.IsNew)
            {
                if (rsvp.Admin != null)
                    return this.email == rsvp.Admin.email;
                else
                    return false;
            }
            else
                return true;
        }

        public bool isAdminForExternalUser(string otherEmail, string inviteType)
        {
            RSVP rsvp = new RSVP(new ExternalConferenceUser(otherEmail), inviteType);
            return this.email == rsvp.Admin.email;
        }

        public DataTable getAdminForUsersList(int conferenceID, string inviteType)
        {
            DataTable table = dataUtil.webAppTable("sp_GetAdminForUsers", new GenericCmdParameter[] { 
                new GenericCmdParameter("@conferenceID", conferenceID),
                new GenericCmdParameter("@adminEmail", this.email) });
            return table;
        }

        protected bool isSuccess(object retval)
        {
            if (retval == null)
                return false;
            else
                return (Convert.ToInt32(retval) == 0);
        }

        //public methods
        public abstract bool Update();
        public abstract HttpCookie ToCookie();
        public abstract DataTable ToTable();

        public abstract bool isPresenter();

    }

    public class KTConferenceUser : ConferenceUser
    {
        private string login;
        private string employeeID;
        private string department;
        private String division;
        private string jobRole;
        private string country;
        private string city;
        private string homeOffice;
        private string shirtSize;
        private string bio;
        private bool invalidUser;
        private int inviteClass = 0;

        private SpeakerAttributes speakerAttribs = null;

        public String Login
        {
            get { return login; }
        }
        public String EmployeeID
        {
            get { return employeeID; }
        }
        public String JobRole
        {
            get { return jobRole; }
            set { jobRole = value; OnPropertyChanged(); }
        }
        public String Department
        {
            get { return department; }
        }
        public String Division
        {
            get
            { return division; }
            set
            {
                division = value; OnPropertyChanged();
            }
        }
        public String Country
        {
            get { return country; }
        }
        public String City
        {
            get { return city; }
        }
        public String HomeOffice
        {
            get { return homeOffice; }
            set { homeOffice = value; OnPropertyChanged(); }
        }
        public String ShirtSize
        {
            get { return shirtSize; }
            set { shirtSize = value; OnPropertyChanged(); }
        }
        public String Bio
        {
            get { return bio; }
            set { bio = value; OnPropertyChanged(); }
        }
        public bool ValidUser
        {
            get { return !invalidUser; }
        }





        public int InviteClass
        {
            get { return inviteClass; }
        }

        //Constructors
        public KTConferenceUser(KTActiveDirectoryUser user)
            : this(user.Email)
        {
        }

        public KTConferenceUser(String userKTEmail)
        {

            DataUtilities.SQLServer.WebDataUtility util =
                    DataUtilities.SQLServer.WebDataUtility.Instance;

            DataTable ktUserTable = getUserData(userKTEmail);



            if (ktUserTable.Rows.Count == 0)
            {
                //if we're here then we have a new KT Employee who's not in tbl_Users yet.
                //add as user then add as KTUser

                loadFromKTLogin(new KTActiveDirectoryUser(userKTEmail));
                Update();
                ktUserTable = getUserData(userKTEmail);
            }
            else if (ktUserTable.Rows[0]["userEmployeeID"].Equals(DBNull.Value) || ktUserTable.Rows[0]["userID"].Equals(DBNull.Value))
            {
                //data missing from both/either tbl_Users and/or tbl_KTUsers

                loadFromKTLogin(new KTActiveDirectoryUser(userKTEmail));
                Update();
                ktUserTable = getUserData(userKTEmail);
            }

            DataRow row = ktUserTable.Rows[0];
            userID = (row["userID"] as int?) ?? 0; //Convert.ToInt32(row["userID"]);
            email = Convert.ToString(row["userEmail"]);
            FirstName = Convert.ToString(row["userFirstName"]);
            LastName = Convert.ToString(row["userLastName"]);
            WorkPhone = Convert.ToString(row["userWorkPhone"]);
            MobilePhone = Convert.ToString(row["userMobilePhone"]);
            FoodAllergies = Convert.ToString(row["userFoodAllergies"]).Trim();
            SpecialNeeds = Convert.ToString(row["userSpecialNeeds"]).Trim();

            login = Convert.ToString(row["userLogin"]);
            department = Convert.ToString(row["userDepartment"]).Trim();
            JobRole = Convert.ToString(row["userJobRole"]);
            Division = Convert.ToString(row["userDivision"]);
            country = Convert.ToString(row["userCountry"]);
            city = Convert.ToString(row["userCity"]);
            HomeOffice = Convert.ToString(row["userHomeOffice"]);
            employeeID = Convert.ToString(row["useremployeeID"]);
            ShirtSize = Convert.ToString(row["userShirtSize"]);
            Bio = Convert.ToString(row["userBio"]);

            isInvitee = Convert.ToBoolean(row["isInvitee"].Equals(DBNull.Value) ? false : row["isInvitee"]);
            inviteClass = Convert.ToInt32(row["inviteClass"].Equals(DBNull.Value) ? -1 : row["inviteClass"]);

            SpeakerAttributes speakerAddons = new SpeakerAttributes(email);

            if (speakerAddons.HasData)
                speakerAttribs = speakerAddons;
        }

        public KTConferenceUser(HttpCookie userCookie)
        {
            if (isValidCookie(userCookie))
                loadFromCookie(userCookie);
        }

        public KTConferenceUser(int userID)
        {
            DataTable table = WebDataUtility.Instance.webAppTable("sp_GetKTUserFromID",new GenericCmdParameter[] { new GenericCmdParameter("@userID", userID)});


            DataTable ktUserTable = getUserData(table.Rows[0]["userEmail"].ToString());

            DataRow row = ktUserTable.Rows[0];
            userID = (row["userID"] as int?) ?? 0; //Convert.ToInt32(row["userID"]);
            email = Convert.ToString(row["userEmail"]);
            FirstName = Convert.ToString(row["userFirstName"]);
            LastName = Convert.ToString(row["userLastName"]);
            WorkPhone = Convert.ToString(row["userWorkPhone"]);
            MobilePhone = Convert.ToString(row["userMobilePhone"]);
            FoodAllergies = Convert.ToString(row["userFoodAllergies"]).Trim();
            SpecialNeeds = Convert.ToString(row["userSpecialNeeds"]).Trim();

            login = Convert.ToString(row["userLogin"]);
            department = Convert.ToString(row["userDepartment"]).Trim();
            JobRole = Convert.ToString(row["userJobRole"]);
            Division = Convert.ToString(row["userDivision"]);
            country = Convert.ToString(row["userCountry"]);
            city = Convert.ToString(row["userCity"]);
            HomeOffice = Convert.ToString(row["userHomeOffice"]);
            employeeID = Convert.ToString(row["useremployeeID"]);
            ShirtSize = Convert.ToString(row["userShirtSize"]);
            Bio = Convert.ToString(row["userBio"]);

            isInvitee = Convert.ToBoolean(row["isInvitee"].Equals(DBNull.Value) ? false : row["isInvitee"]);
            inviteClass = Convert.ToInt32(row["inviteClass"].Equals(DBNull.Value) ? -1 : row["inviteClass"]);

            SpeakerAttributes speakerAddons = new SpeakerAttributes(email);

            if (speakerAddons.HasData)
                speakerAttribs = speakerAddons;

        }

        //Public Methods
        public override bool Update()
        {
            object retval = false;

            if (userID < 0) //new user - add to database
            {
                try
                {
                    dataUtil.webAppScalar("sp_LoadUser",
                        new GenericCmdParameter[] { new GenericCmdParameter("@user", UserTable()) }, ref retval);

                    if (retval != null)
                        userID = Convert.ToInt32(retval);
                }
                catch (Exception e)
                {
                    userID = -1;
                    return false; //couldn't load base user. client should write cookie.
                }
            }
            else
            {
                dataUtil.webAppCmd("sp_LoadUser", new GenericCmdParameter[] { new GenericCmdParameter("@user", UserTable()) }, ref retval);

                if (!isSuccess(retval))
                {
                    return false;
                    //userUpdate failed. client should write cookie.
                }
            }
            dataUtil.webAppCmd("sp_LoadKTUser", new GenericCmdParameter[] { new GenericCmdParameter("@user", ToTable()) }, ref retval);


            if (speakerAttribs != null)
                speakerAttribs.Upload();

            return isSuccess(retval); //if false, client shoudl write cookie
        }

        public override DataTable ToTable()
        {
            DataTable retval = new DataTable("user");

            retval.Columns.Add("userID", typeof(int));
            retval.Columns.Add("userLogin", typeof(string));
            retval.Columns.Add("userEmployeeID", typeof(string));
            retval.Columns.Add("userJobRole", typeof(string));
            retval.Columns.Add("userHomeOffice", typeof(string));
            retval.Columns.Add("userCity", typeof(string));
            retval.Columns.Add("userCountry", typeof(string));
            retval.Columns.Add("userDepartment", typeof(string));
            retval.Columns.Add("userDivision", typeof(string));
            retval.Columns.Add("userShirtSize", typeof(string));
            retval.Columns.Add("userBio", typeof(string));
            retval.Columns.Add("lastUpdated", typeof(DateTime));

            DataRow newRow = retval.NewRow();
            newRow["userID"] = this.userID;
            newRow["userLogin"] = this.login;
            newRow["userEmployeeID"] = this.employeeID;
            newRow["userJobRole"] = this.jobRole;
            newRow["userHomeOffice"] = this.homeOffice;
            newRow["userCity"] = this.city;
            newRow["userCountry"] = this.country;
            newRow["userDepartment"] = this.department;
            newRow["userDivision"] = this.division;
            newRow["userShirtSize"] = this.shirtSize;
            newRow["userBio"] = this.bio;
            newRow["lastUpdated"] = DateTime.Now;
            retval.Rows.Add(newRow);
            return retval;
        }

        public override HttpCookie ToCookie()
        {
            HttpCookie cookie = new HttpCookie("KTConferenceUser");

            DataTable user = UserTable();
            DataTable data = ToTable();

            foreach (DataRow row in user.Rows)
            {
                foreach (DataColumn col in user.Columns)
                    cookie[col.ColumnName] = Convert.ToString(row[col.ColumnName]);
            }

            foreach (DataRow row in data.Rows)
            {
                foreach (DataColumn col in data.Columns)
                    cookie[col.ColumnName] = Convert.ToString(row[col.ColumnName]);
            }

            cookie.Expires = DateTime.Now.AddDays(1);

            return cookie;
        }

        public bool IsRegistered(int conferenceID)
        {
            object registered = null;

            dataUtil.webAppScalar("sp_GetIsRegistered", new GenericCmdParameter[] {
                new GenericCmdParameter("@conferenceID", conferenceID),
                new GenericCmdParameter("@userEmail", email)}, ref registered);

            return Convert.ToBoolean(registered);

        }

        public bool IsConferenceAdministrator()
        {
            object retval = null;

            dataUtil.webAppScalar("sp_IsConferenceAdmin", new GenericCmdParameter[] { 
                new GenericCmdParameter("@userID", this.userID) , 
                new GenericCmdParameter("@conferenceID", Conference.Instance.ID)
                },
                ref retval);

            return Convert.ToBoolean(retval);
        }
        public override bool isPresenter()
        {
            object retval = null;

            dataUtil.webAppScalar("sp_isConferenceSpeaker", new GenericCmdParameter[] { 
                new GenericCmdParameter("@userID", this.userID) , 
                new GenericCmdParameter("@conferenceID", Conference.Instance.ID)
                },
                ref retval);

            return Convert.ToBoolean(retval);
        }

        public void SetSpeakerAttributes(MemberAttributesCollection attribs)
        {
            this.speakerAttribs = new SpeakerAttributes(attribs);
        }

        //Private Methods

        private DataTable getUserData(string userKTEmail)
        {
            DataUtilities.SQLServer.WebDataUtility util =
                    DataUtilities.SQLServer.WebDataUtility.Instance;

            return util.webAppTable("sp_GetKTUser",
                new DataUtilities.SQLServer.GenericCmdParameter[] { 
                    new DataUtilities.SQLServer.GenericCmdParameter("@userKTEmail", userKTEmail),
                    new DataUtilities.SQLServer.GenericCmdParameter("@conferenceID", Conference.Instance.ID)
                });
        }

        private void loadFromKTLogin(KTActiveDirectoryUser KTLogin)
        {
            try
            {
                if (KTLogin.Login != string.Empty)
                {
                    login = KTLogin.Login;

                    firstName = KTLogin.FirstName;
                    lastName = KTLogin.LastName;
                    email = KTLogin.Email;


                    employeeID = KTLogin.EmployeeID;
                    department = KTLogin.Department;
                    division = KTLogin.Division;
                    city = KTLogin.City;
                    country = KTLogin.Country;
                    invalidUser = false;

                }
                else
                    invalidUser = true;
            }
            catch (Exception e)
            {
                invalidUser = true;
            }
        }

        private void loadFromCookie(HttpCookie cookie)
        {
            if (cookie.Name == "KTConferenceUser")
            {
                foreach (KeyValuePair<string, string> pair in cookie.Values)
                {
                    switch (pair.Key)
                    {
                        case "userKTEmail":
                            {
                                email = pair.Value;
                                break;
                            }
                        case "userLogin":
                            {
                                login = pair.Value;
                                break;
                            }
                        case "userEmployeeID":
                            {
                                employeeID = pair.Value;
                                break;
                            }
                        case "userFirstName":
                            {
                                firstName = pair.Value;
                                break;
                            }
                        case "userLastName":
                            {
                                lastName = pair.Value;
                                break;
                            }
                        case "userJobRole":
                            {
                                jobRole = pair.Value;
                                break;
                            }
                        case "userHomeOffice":
                            {
                                homeOffice = pair.Value;
                                break;
                            }
                        case "userCity":
                            {
                                city = pair.Value;
                                break;
                            }
                        case "userCountry":
                            {
                                country = pair.Value;
                                break;
                            }
                        case "userDepartment":
                            {
                                department = pair.Value;
                                break;
                            }
                        case "userDivision":
                            {
                                division = pair.Value;
                                break;
                            }
                        case "userWorkPhone":
                            {
                                workPhone = pair.Value;
                                break;
                            }
                        case "userMobilePhone":
                            {
                                mobilePhone = pair.Value;
                                break;
                            }
                        case "userFoodAllergies":
                            {
                                foodAllergies = pair.Value;
                                break;
                            }
                        case "userSpecialNeeds":
                            {
                                specialNeeds = pair.Value;
                                break;
                            }
                        case "userShirtSize":
                            {
                                shirtSize = pair.Value;
                                break;
                            }
                        case "userBio":
                            {
                                bio = pair.Value;
                                break;
                            }

                    }
                }
            }
        }

        private bool isValidCookie(HttpCookie cookie)
        {
            if (cookie.Name == "KTConferenceUser")
            {
                if (cookie.Values["userKTEmail"] != null)
                {
                    KTActiveDirectoryUser test = new KTActiveDirectoryUser(cookie.Values["userKTEmail"]);
                    return (test.Login != String.Empty);
                }
            }
            return false;
        }

    }

    public class ExternalConferenceUser : ConferenceUser
    {
        protected Vendor _vendor = null;
        private string staffRole = string.Empty;

        private SpeakerAttributes speakerAttribs = null;

        public Vendor ParentVendor
        {
            get { return _vendor; }
            set { _vendor = value; }
        }

        public string StaffRole
        {
            get { return staffRole; }
            set { staffRole = value; }
        }

        //constructors
        public ExternalConferenceUser(MemberAttributesCollection attributes, Vendor vendor)
        {

            loadAttributes(attributes);
            _vendor = vendor;

        }

        //this only good for exsiting conference users. not for creating a new one.
        public ExternalConferenceUser(string email)
        {

            DataTable table = WebDataUtility.Instance.webAppTable("sp_GetExternalStaffUser",
                new GenericCmdParameter[] { new GenericCmdParameter("@conferenceID", Conference.Instance.ID),
                                            new GenericCmdParameter("@userEmail", email) });
            MemberAttributesCollection attribs = new MemberAttributesCollection();

            foreach (DataColumn col in table.Columns)
                attribs.Add(new MemberAttribute(col.ColumnName, table.Rows[0][col.ColumnName]));

            if (table.Rows.Count == 0)
                throw new IndexOutOfRangeException("No external user by that email is invited to conference.");

            loadAttributes(attribs);

            SpeakerAttributes speakerAddons = new SpeakerAttributes(email);

            if (speakerAddons.HasData)
                speakerAttribs = speakerAddons;
        }

        //public methods

        public void AssignVendor(Vendor vendor)
        {
            if (vendor == null)
                return;

            if(_vendor != null)
            {
                if(!_vendor.VendorID.Equals(vendor.VendorID))
                    _vendor = vendor;
            }
            else
                _vendor = vendor;
        }
        
        public override HttpCookie ToCookie()
        {

            HttpCookie cookie = new HttpCookie("ExternalStaffUser");
            DataTable user = UserTable();
            DataTable extStaff = ToTable();

            foreach (DataRow row in user.Rows)
            {
                foreach (DataColumn col in user.Columns)
                    cookie[col.ColumnName] = Convert.ToString(row[col.ColumnName]);
            }
            foreach (DataRow row in extStaff.Rows)
            {
                foreach (DataColumn col in extStaff.Columns)
                    cookie[col.ColumnName] = Convert.ToString(row[col.ColumnName]);
            }

            cookie.Expires = DateTime.Now.AddDays(1);
            return cookie;


        }

        public override bool Update()
        {
            object retval = null;

            if (userID < 0)
            {
                try
                {
                    dataUtil.webAppScalar("sp_LoadUser",
                            new GenericCmdParameter[] { new GenericCmdParameter("@user", UserTable()) }, ref retval);
                    if (retval != null)
                        userID = Convert.ToInt32(retval);
                }
                catch (Exception e)
                {
                    userID = -1;
                    return false;
                }
            }
            else
            {
                    dataUtil.webAppScalar("sp_LoadUser",
                            new GenericCmdParameter[] { new GenericCmdParameter("@user", UserTable()) }, ref retval);

                    if (!isSuccess(retval))
                    {
                        return false;
                        //userUpdate failed. client should write cookie.
                    }
              }


                dataUtil.webAppCmd("sp_LoadExternalStaff",
                    new GenericCmdParameter[] { new GenericCmdParameter("@extStaff", ToTable()) }, ref retval);


            if (speakerAttribs != null)
                speakerAttribs.Upload();

            return isSuccess(retval);
        }

        public override DataTable ToTable()
        {
            DataTable retval = new DataTable("user");

            retval.Columns.Add("userID", typeof(int));
            retval.Columns.Add("vendorID", typeof(int));
            retval.Columns.Add("conferenceID", typeof(int));
            retval.Columns.Add("staffRole", typeof(string));
            retval.Columns.Add("lastUpdated", typeof(DateTime));

            DataRow newRow = retval.NewRow();
            newRow["userID"] = this.userID;
            newRow["vendorID"] = this._vendor.VendorID;
            newRow["conferenceID"] = Conference.Instance.ID;
            newRow["staffRole"] = this.staffRole;
            newRow["lastUpdated"] = DateTime.Now;
            retval.Rows.Add(newRow);
            return retval;
        }

        public void SetSpeakerAttributes(MemberAttributesCollection attribs)
        {
            this.speakerAttribs = new SpeakerAttributes(attribs);
        }

        public override bool isPresenter()
        {
            return false;
        }

        private void loadAttributes(MemberAttributesCollection attributes)
        {
            foreach (MemberAttribute attr in attributes)
            {
                switch (attr.Name)
                {
                    case ("userID"):
                        {
                            userID = DBNullable.ToInt(attr.Value);
                            break;
                        }
                    case ("userEmail"):
                        {
                            email = DBNullable.ToString(attr.Value);
                            break;
                        }
                    case ("userFirstName"):
                        {
                            firstName = DBNullable.ToString(attr.Value);
                            break;
                        }
                    case ("userLastName"):
                        {
                            lastName = DBNullable.ToString(attr.Value);
                            break;
                        }
                    case ("userWorkPhone"):
                        {
                            workPhone = DBNullable.ToString(attr.Value);
                            break;
                        }
                    case ("userMobilePhone"):
                        {
                            mobilePhone = DBNullable.ToString(attr.Value);
                            break;
                        }
                    case ("userFoodAllergies"):
                        {
                            foodAllergies = DBNullable.ToString(attr.Value);
                            break;
                        }
                    case ("userSpecialNeeds"):
                        {
                            specialNeeds = DBNullable.ToString(attr.Value);
                            break;
                        }
                    case ("staffRole"):
                        {
                            staffRole = DBNullable.ToString(attr.Value);
                            break;
                        }
                    case("vendorID"):
                        {
                            if(_vendor == null)
                                _vendor = new Vendor(DBNullable.ToInt(attr.Value));
                            else if (!_vendor.VendorID.Equals(DBNullable.ToInt(attr.Value)))
                                _vendor = new Vendor(DBNullable.ToInt(attr.Value));
                            break;
                        }
                }
            }

            isInvitee = true;

        }

    }

    public class Vendor
    {
        //members
        private DataTable _table = new DataTable("vendor");

        private bool _rowCount
        {
            get { return _table.Rows.Count == 1; }
        }
        public int VendorID
        {
            get
            {
                if (_rowCount)
                {
                    try
                    {
                        return Convert.ToInt32((_table.Rows[0]["vendorID"].Equals(DBNull.Value) ?
                            0 : _table.Rows[0]["vendorID"]));

                    }
                    catch (Exception e)
                    {
                        return 0;
                    }
                }
                return 0;
            }
        }
        public string VendorName
        {
            get
            {
                if (_rowCount)
                {
                    try
                    {
                        return Convert.ToString((_table.Rows[0]["vendorCompanyName"].Equals(DBNull.Value) ?
                            string.Empty : _table.Rows[0]["vendorCompanyName"]));
                    }
                    catch (Exception e)
                    {
                        return string.Empty;
                    }
                }
                return string.Empty;
            }

            set
            {
                if (_rowCount)
                {
                    DataRow row = _table.Rows[0];
                    row.BeginEdit();
                    row["vendorCompanyName"] = value;
                    row.AcceptChanges();
                    row.EndEdit();
                }
                else
                {
                    throw new RowNotInTableException("Problem with vendor table - row doesn't exist or more than one row.");
                }
            }

        }
        public string StreetAddress
        {
            get
            {
                if (_rowCount)
                {
                    try
                    {
                        return Convert.ToString((_table.Rows[0]["vendorStreetAddress"].Equals(DBNull.Value) ?
                            string.Empty : _table.Rows[0]["vendorStreetAddress"]));
                    }
                    catch (Exception e)
                    {
                        return string.Empty;
                    }
                }
                return string.Empty;
            }

            set
            {
                if (_rowCount)
                {
                    DataRow row = _table.Rows[0];
                    row.BeginEdit();
                    row["vendorStreetAddress"] = value;
                    row.AcceptChanges();
                    row.EndEdit();
                }
                else
                {
                    throw new RowNotInTableException("Problem with vendor table - row doesn't exist or more than one row.");
                }
            }

        }
        public string City
        {
            get
            {
                if (_rowCount)
                {
                    try
                    {
                        return Convert.ToString((_table.Rows[0]["vendorCity"].Equals(DBNull.Value) ?
                            string.Empty : _table.Rows[0]["vendorCity"]));
                    }
                    catch (Exception e)
                    {
                        return string.Empty;
                    }
                }
                return string.Empty;
            }

            set
            {
                if (_rowCount)
                {
                    DataRow row = _table.Rows[0];
                    row.BeginEdit();
                    row["vendorCity"] = value;
                    row.AcceptChanges();
                    row.EndEdit();
                }
                else
                {
                    throw new RowNotInTableException("Problem with vendor table - row doesn't exist or more than one row.");
                }
            }

        }
        public string State
        {
            get
            {
                if (_rowCount)
                {
                    try
                    {
                        return Convert.ToString((_table.Rows[0]["vendorState"].Equals(DBNull.Value) ?
                            string.Empty : _table.Rows[0]["vendorState"]));
                    }
                    catch (Exception e)
                    {
                        return string.Empty;
                    }
                }
                return string.Empty;
            }

            set
            {
                if (_rowCount)
                {
                    DataRow row = _table.Rows[0];
                    row.BeginEdit();
                    row["vendorState"] = value;
                    row.AcceptChanges();
                    row.EndEdit();
                }
                else
                {
                    throw new RowNotInTableException("Problem with vendor table - row doesn't exist or more than one row.");
                }
            }

        }
        public string ZipCode
        {
            get
            {
                if (_rowCount)
                {
                    try
                    {
                        return Convert.ToString((_table.Rows[0]["vendorZip"].Equals(DBNull.Value) ?
                            string.Empty : _table.Rows[0]["vendorZip"]));
                    }
                    catch (Exception e)
                    {
                        return string.Empty;
                    }
                }
                return string.Empty;
            }

            set
            {
                if (_rowCount)
                {
                    DataRow row = _table.Rows[0];
                    row.BeginEdit();
                    row["vendorZip"] = value;
                    row.AcceptChanges();
                    row.EndEdit();
                }
                else
                {
                    throw new RowNotInTableException("Problem with vendor table - row doesn't exist or more than one row.");
                }
            }

        }
        public string Country
        {
            get
            {
                if (_rowCount)
                {
                    try
                    {
                        return Convert.ToString((_table.Rows[0]["vendorCountry"].Equals(DBNull.Value) ?
                            string.Empty : _table.Rows[0]["vendorCountry"]));
                    }
                    catch (Exception e)
                    {
                        return string.Empty;
                    }
                }
                return string.Empty;
            }

            set
            {
                if (_rowCount)
                {
                    DataRow row = _table.Rows[0];
                    row.BeginEdit();
                    row["vendorCountry"] = value;
                    row.AcceptChanges();
                    row.EndEdit();
                }
                else
                {
                    throw new RowNotInTableException("Problem with vendor table - row doesn't exist or more than one row.");
                }
            }

        }
        public string ContactName
        {
            get
            {
                if (_rowCount)
                {
                    try
                    {
                        return Convert.ToString((_table.Rows[0]["vendorContactName"].Equals(DBNull.Value) ?
                            string.Empty : _table.Rows[0]["vendorContactName"]));
                    }
                    catch (Exception e)
                    {
                        return string.Empty;
                    }
                }
                return string.Empty;
            }

            set
            {
                if (_rowCount)
                {
                    DataRow row = _table.Rows[0];
                    row.BeginEdit();
                    row["vendorContactName"] = value;
                    row.AcceptChanges();
                    row.EndEdit();
                }
                else
                {
                    throw new RowNotInTableException("Problem with vendor table - row doesn't exist or more than one row.");
                }
            }

        }
        public string ContactPhone
        {
            get
            {
                if (_rowCount)
                {
                    try
                    {
                        return Convert.ToString((_table.Rows[0]["vendorContactPhone"].Equals(DBNull.Value) ?
                            string.Empty : _table.Rows[0]["vendorContactPhone"]));
                    }
                    catch (Exception e)
                    {
                        return string.Empty;
                    }
                }
                return string.Empty;
            }

            set
            {
                if (_rowCount)
                {
                    DataRow row = _table.Rows[0];
                    row.BeginEdit();
                    row["vendorContactPhone"] = value;
                    row.AcceptChanges();
                    row.EndEdit();
                }
                else
                {
                    throw new RowNotInTableException("Problem with vendor table - row doesn't exist or more than one row.");
                }
            }

        }
        public string ContactEmail
        {
            get
            {
                if (_rowCount)
                {
                    try
                    {
                        return Convert.ToString((_table.Rows[0]["vendorContactEmail"].Equals(DBNull.Value) ?
                            string.Empty : _table.Rows[0]["vendorContactEmail"]));
                    }
                    catch (Exception e)
                    {
                        return string.Empty;
                    }
                }
                return string.Empty;
            }

            set
            {
                if (_rowCount)
                {
                    DataRow row = _table.Rows[0];
                    row.BeginEdit();
                    row["vendorContactEmail"] = value;
                    row.AcceptChanges();
                    row.EndEdit();
                }
                else
                {
                    throw new RowNotInTableException("Problem with vendor table - row doesn't exist or more than one row.");
                }
            }

        }
        public string WebAddress
        {
            get
            {
                if (_rowCount)
                {
                    try
                    {
                        return Convert.ToString((_table.Rows[0]["vendorWebAddress"].Equals(DBNull.Value) ?
                            string.Empty : _table.Rows[0]["vendorWebAddress"]));
                    }
                    catch (Exception e)
                    {
                        return string.Empty;
                    }
                }
                return string.Empty;
            }

            set
            {
                if (_rowCount)
                {
                    DataRow row = _table.Rows[0];
                    row.BeginEdit();
                    row["vendorWebAddress"] = value;
                    row.AcceptChanges();
                    row.EndEdit();
                }
                else
                {
                    throw new RowNotInTableException("Problem with vendor table - row doesn't exist or more than one row.");
                }
            }

        }
        public string Function
        {
            get
            {
                if (_rowCount)
                {
                    try
                    {
                        return Convert.ToString((_table.Rows[0]["vendorFunction"].Equals(DBNull.Value) ?
                            string.Empty : _table.Rows[0]["vendorFunction"]));
                    }
                    catch (Exception e)
                    {
                        return string.Empty;
                    }
                }
                return string.Empty;
            }

            set
            {
                if (_rowCount)
                {
                    DataRow row = _table.Rows[0];
                    row.BeginEdit();
                    row["vendorFunction"] = value;
                    row.AcceptChanges();
                    row.EndEdit();
                }
                else
                {
                    throw new RowNotInTableException("Problem with vendor table - row doesn't exist or more than one row.");
                }
            }

        }

        protected WebDataUtility dataUtil = WebDataUtility.Instance;

        //constructors
        public Vendor(int id)
        {
            load(id);
        }

        public Vendor(MemberAttributesCollection attributes)
        {
            createEmptyVendorTable();
            foreach (MemberAttribute attribute in attributes)
                setValue(attribute);
        }

        //private methods
        protected void load(int id)
        {
            _table = dataUtil.webAppTable("sp_GetVendor", new GenericCmdParameter[] { new GenericCmdParameter("@vendorID", id) });
        }

        private void createEmptyVendorTable()
        {
            _table.Columns.Add("vendorID", typeof(int));
            _table.Columns.Add("vendorCompanyName", typeof(string));
            _table.Columns.Add("vendorStreetAddress", typeof(string));
            _table.Columns.Add("vendorCity", typeof(string));
            _table.Columns.Add("vendorState", typeof(string));
            _table.Columns.Add("vendorZip", typeof(string));
            _table.Columns.Add("vendorCountry", typeof(string));
            _table.Columns.Add("vendorContactName", typeof(string));
            _table.Columns.Add("vendorContactPhone", typeof(string));
            _table.Columns.Add("vendorContactEmail", typeof(string));
            _table.Columns.Add("vendorWebAddress", typeof(string));
            _table.Columns.Add("vendorFunction", typeof(string));
        }

        protected void setValue(MemberAttribute attribute)
        {
            switch (attribute.Name)
            {
                case "VendorName":
                    {
                        VendorName = Convert.ToString(attribute.Value);
                        return;
                    }
                case "VendorWeb":
                    {
                        WebAddress = Convert.ToString(attribute.Value);
                        return;
                    }
                case "vendorPOCName":
                    {
                        ContactName = Convert.ToString(attribute.Value);
                        return;
                    }
                case "vendorPOCEmail":
                    {
                        ContactEmail = Convert.ToString(attribute.Value);
                        return;
                    }
                case "vendorStreetAddress":
                    {
                        StreetAddress = Convert.ToString(attribute.Value);
                        return;
                    }
                case "vendorPhone":
                    {
                        ContactPhone = Convert.ToString(attribute.Value);
                        return;
                    }
                case "vendorFunction":
                    {
                        Function = Convert.ToString(attribute.Value);
                        return;
                    }
                default:
                    {
                        throw new IndexOutOfRangeException("'" + attribute.Name + "' is not a member of the Vendor Object");
                    }
            }
        }

        //public methods
        public DataTable ToTable()
        {
            return _table;

        }

        public bool Update()
        {
            object retval = false;

            dataUtil.webAppCmd("sp_LoadVendor", new GenericCmdParameter[] { new GenericCmdParameter("@vendor", ToTable()) }, ref retval);

            return (retval as bool?) ?? false; // Convert.ToBoolean(retval);

        }
    }

    public class Venue : Vendor
    {

        private DataTable _venuetable = new DataTable();
        private bool _rowCount
        {
            get { return _venuetable.Rows.Count == 1; }
        }

        public string VenueType
        {
            get
            {
                if (_rowCount)
                {
                    try
                    {
                        return Convert.ToString((_venuetable.Rows[0]["venueType"].Equals(DBNull.Value) ?
                            string.Empty : _venuetable.Rows[0]["venueType"]));
                    }
                    catch (Exception e)
                    {
                        return string.Empty;
                    }
                }
                return string.Empty;
            }

            set
            {
                if (_rowCount)
                {
                    DataRow row = _venuetable.Rows[0];
                    row.BeginEdit();
                    row["venueType"] = value;
                    row.AcceptChanges();
                    row.EndEdit();
                }
                else
                {
                    throw new RowNotInTableException("Problem with vendor table - row doesn't exist or more than one row.");
                }
            }
        }

        public string MainPhone
        {
            get
            {
                if (_rowCount)
                {
                    try
                    {
                        return Convert.ToString((_venuetable.Rows[0]["venueMainPhone"].Equals(DBNull.Value) ?
                            string.Empty : _venuetable.Rows[0]["venueMainPhone"]));
                    }
                    catch (Exception e)
                    {
                        return string.Empty;
                    }
                }
                return string.Empty;
            }

            set
            {
                if (_rowCount)
                {
                    DataRow row = _venuetable.Rows[0];
                    row.BeginEdit();
                    row["venueMainPhone"] = value;
                    row.AcceptChanges();
                    row.EndEdit();
                }
                else
                {
                    throw new RowNotInTableException("Problem with vendor table - row doesn't exist or more than one row.");
                }
            }
        }

        public string MapHyperlink
        {
            get
            {
                if (_rowCount)
                {
                    try
                    {
                        return Convert.ToString((_venuetable.Rows[0]["venueMapHyperlink"].Equals(DBNull.Value) ?
                            string.Empty : _venuetable.Rows[0]["venueMapHyperlink"]));
                    }
                    catch (Exception e)
                    {
                        return string.Empty;
                    }
                }
                return string.Empty;
            }

            set
            {
                if (_rowCount)
                {
                    DataRow row = _venuetable.Rows[0];
                    row.BeginEdit();
                    row["venueMapHyperlink"] = value;
                    row.AcceptChanges();
                    row.EndEdit();
                }
                else
                {
                    throw new RowNotInTableException("Problem with vendor table - row doesn't exist or more than one row.");
                }
            }
        }

        public Venue(int vendorID)
            : base(vendorID)
        {
            _venuetable = dataUtil.webAppTable("sp_GetVenue", new GenericCmdParameter[] { new GenericCmdParameter("@vendorID", vendorID) });
        }

        public DataTable ToTable()
        {
            return _venuetable;
        }


    }

    public class Conference
    {
        private WebDataUtility dataUtil = WebDataUtility.Instance;
        private DataTable _table = new DataTable("conferenceMetaData");
        private bool _rowCount
        {
            get { return _table.Rows.Count == 1; }
        }
        private Venue _venue = null;

        public int ID
        {
            get
            {
                if (_rowCount)
                {
                    try
                    {
                        return Convert.ToInt32((_table.Rows[0]["conferenceID"].Equals(DBNull.Value) ?
                            0 : _table.Rows[0]["conferenceID"]));
                    }
                    catch (Exception e)
                    {
                        return 0;
                    }
                }
                return 0;
            }

        }
        public Venue Venue
        {
            get
            {
                return _venue;
            }

            set { _venue = value; }

        }
        public string ConferenceTitle
        {
            get
            {
                if (_rowCount)
                {
                    try
                    {
                        return Convert.ToString((_table.Rows[0]["conferenceTitle"].Equals(DBNull.Value) ?
                            string.Empty : _table.Rows[0]["conferenceTitle"]));
                    }
                    catch (Exception e)
                    {
                        return string.Empty;
                    }
                }
                return string.Empty;
            }
        }
        public string Website
        {
            get
            {
                if (_rowCount)
                {
                    try
                    {
                        return Convert.ToString((_table.Rows[0]["conferenceWebSite"].Equals(DBNull.Value) ?
                            string.Empty : _table.Rows[0]["conferenceWebSite"]));
                    }
                    catch (Exception e)
                    {
                        return string.Empty;
                    }
                }
                return string.Empty;
            }
        }
        public int InviteMax
        {
            get
            {
                if (_rowCount)
                {
                    try
                    {
                        return Convert.ToInt32((_table.Rows[0]["conferenceInviteeMax"].Equals(DBNull.Value) ?
                            0 : _table.Rows[0]["conferenceInviteeMax"]));
                    }
                    catch (Exception e)
                    {
                        return 0;
                    }
                }
                return 0;
            }
        }
        public DateTime Start
        {
            get
            {
                if (_rowCount)
                {
                    try
                    {
                        return Convert.ToDateTime((_table.Rows[0]["conferenceStartTime"].Equals(DBNull.Value) ?
                             new DateTime(1900, 1, 1) : _table.Rows[0]["conferenceStartTime"]));
                    }
                    catch (Exception e)
                    {
                        return new DateTime(1900, 1, 1);
                    }
                }
                return new DateTime(1900, 1, 1);
            }
        }
        public DateTime Stop
        {
            get
            {
                if (_rowCount)
                {
                    try
                    {
                        return Convert.ToDateTime((_table.Rows[0]["conferenceEndTime"].Equals(DBNull.Value) ?
                             new DateTime(1900, 1, 1) : _table.Rows[0]["conferenceEndTime"]));
                    }
                    catch (Exception e)
                    {
                        return new DateTime(1900, 1, 1);
                    }
                }
                return new DateTime(1900, 1, 1);
            }
        }
        public DateTime PrimaryRegistrationOpen
        {
            get
            {
                if (_rowCount)
                {
                    try
                    {
                        return Convert.ToDateTime((_table.Rows[0]["conferenceRegistrationOpen"].Equals(DBNull.Value) ?
                             new DateTime(1900, 1, 1) : _table.Rows[0]["conferenceRegistrationOpen"]));
                    }
                    catch (Exception e)
                    {
                        return new DateTime(1900, 1, 1);
                    }
                }
                return new DateTime(1900, 1, 1);
            }

        }
        public DateTime PrimaryRegistrationClosed
        {
            get
            {
                if (_rowCount)
                {
                    try
                    {
                        return Convert.ToDateTime((_table.Rows[0]["conferenceRegistrationClosed"].Equals(DBNull.Value) ?
                             new DateTime(1900, 1, 1) : _table.Rows[0]["conferenceRegistrationClosed"]));
                    }
                    catch (Exception e)
                    {
                        return new DateTime(1900, 1, 1);
                    }
                }
                return new DateTime(1900, 1, 1);
            }
        }
        public DateTime LateRegistrationOpen
        {
            get
            {
                if (_rowCount)
                {
                    try
                    {
                        return Convert.ToDateTime((_table.Rows[0]["conferenceLateRegistrationOpen"].Equals(DBNull.Value) ?
                             new DateTime(1900, 1, 1) : _table.Rows[0]["conferenceLateRegistrationOpen"]));
                    }
                    catch (Exception e)
                    {
                        return new DateTime(1900, 1, 1);
                    }
                }
                return new DateTime(1900, 1, 1);
            }
        }
        public DateTime LateRegistrationClosed
        {
            get
            {
                if (_rowCount)
                {
                    try
                    {
                        return Convert.ToDateTime((_table.Rows[0]["conferenceLateRegistrationClosed"].Equals(DBNull.Value) ?
                             new DateTime(1900, 1, 1) : _table.Rows[0]["conferenceLateRegistrationClosed"]));
                    }
                    catch (Exception e)
                    {
                        return new DateTime(1900, 1, 1);
                    }
                }
                return new DateTime(1900, 1, 1);
            }
        }
        public DateTime WelcomeReceptionStart
        {
            get
            {
                if (_rowCount)
                {
                    try
                    {
                        return Convert.ToDateTime((_table.Rows[0]["conferenceWelcomeReception"].Equals(DBNull.Value) ?
                             new DateTime(1900, 1, 1) : _table.Rows[0]["conferenceWelcomeReception"]));
                    }
                    catch (Exception e)
                    {
                        return new DateTime(1900, 1, 1);
                    }
                }
                return new DateTime(1900, 1, 1);
            }
        }

        public DateTime CurrentRegistrationWindowClosed
        {
            get{
                if (DateTime.Now >= PrimaryRegistrationOpen && DateTime.Now <= PrimaryRegistrationClosed)
                    return PrimaryRegistrationClosed;
                if (DateTime.Now >= LateRegistrationOpen && DateTime.Now <= LateRegistrationClosed)
                    return LateRegistrationClosed;

                return new DateTime(1900, 1, 1);
            }
        }

        public DateTime CheckInStart
        {
            get
            {
                if (_rowCount)
                {
                    try
                    {
                        return Convert.ToDateTime((_table.Rows[0]["conferenceCheckInStart"].Equals(DBNull.Value) ?
                             new DateTime(1900, 1, 1) : _table.Rows[0]["conferenceCheckInStart"]));
                    }
                    catch (Exception e)
                    {
                        return new DateTime(1900, 1, 1);
                    }
                }
                return new DateTime(1900, 1, 1);
            }
        }
        public DateTime CheckInStop
        {
            get
            {
                if (_rowCount)
                {
                    try
                    {
                        return Convert.ToDateTime((_table.Rows[0]["conferenceCheckInStop"].Equals(DBNull.Value) ?
                             new DateTime(1900, 1, 1) : _table.Rows[0]["conferenceCheckInStop"]));
                    }
                    catch (Exception e)
                    {
                        return new DateTime(1900, 1, 1);
                    }
                }
                return new DateTime(1900, 1, 1);
            }
        }
        public DateTime NoChangeDate
        {
            get
            {
                if (_rowCount)
                {
                    try
                    {
                        return Convert.ToDateTime((_table.Rows[0]["conferenceEventsFrozenDate"].Equals(DBNull.Value) ?
                             new DateTime(1900, 1, 1) : _table.Rows[0]["conferenceEventsFrozenDate"]));
                    }
                    catch (Exception e)
                    {
                        return new DateTime(1900, 1, 1);
                    }
                }
                return new DateTime(1900, 1, 1);
            }
        }

        private List<string> _conferencePOCs = new List<string>();
        public List<string> POCs
        {
            get { return _conferencePOCs; }
        }

        private static Conference _instance;

        public static Conference Instance
        {
            get
            {
                if (_instance == null)
                    return new Conference(Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["ConferenceID"]));
                else
                    return _instance;
            }
        }

        private Conference(int conferenceID)
        {
            _table = dataUtil.webAppTable("sp_GetConferenceMetaData",
                new GenericCmdParameter[] { new GenericCmdParameter("@conferenceID", conferenceID) });
            _venue = new Venue(Convert.ToInt32(_table.Rows[0]["venueID"]));

            DataTable pocs = dataUtil.webAppTable("sp_GetConferencePOCs", new GenericCmdParameter[] { new GenericCmdParameter("@conferenceID", conferenceID) });
            foreach (DataRow row in pocs.Rows)
            {
                _conferencePOCs.Add(Convert.ToString(row["userEmail"]));
            }
        }

        public DataTable ToTable()
        {
            return _table;
        }

        public bool IsAdmin(string userEmail)
        {
            foreach (string email in _conferencePOCs)
            {
                if(email.Equals(userEmail))
                    return true;
            }
            return false;
        }

        public void SendInvitationEmails(Control context, int inviteClass, bool testingOnly)
        {
            DataTable tbl = WebDataUtility.Instance.webAppTable("sp_GetInvitees",
                    new GenericCmdParameter[] { 
                        new GenericCmdParameter("@conferenceID", this.ID), 
                        new GenericCmdParameter("@inviteClass", inviteClass) });
            int i = 1;
            DateTime deadline = (inviteClass == 1 ? this.PrimaryRegistrationClosed : (inviteClass == 2 ? this.LateRegistrationClosed : this.NoChangeDate));

            foreach (DataRow row in tbl.Rows)
            {
                try
                {
                    KTConferenceUser user = new KTConferenceUser((testingOnly?System.Configuration.ConfigurationManager.AppSettings["sysadmin"] : Convert.ToString(row["userEmail"])));
                       SendInvitation(context, user, deadline);
                    ExceptionUtility.LogException(new Exception("SUCCESS: Invite sent to " +
                        user.FullName.ToUpper() + ". Total Emails sent: " + (++i)), "ConferenceObjects.ConferenceLibrary.Conference.SendInvitationEmails(int inviteClass)");
                }
                catch (Exception e)
                {
                    ExceptionUtility.LogException(e, "ConferenceObjects.ConferenceLibrary.Conference.SendInvitationEmails(int inviteClass)");

                }
            }
        }

        public void CorrectMissingConfirmationCodes()
        {
            DataTable rsvps = WebDataUtility.Instance.webAppTable("sp_GetRSVPConfirmationBatch",
                new GenericCmdParameter[] { new GenericCmdParameter("@conferenceID", ID), new GenericCmdParameter("@ignoreSentEmails", true) });

            foreach (DataRow row in rsvps.Rows)
            {
                RSVP rsvp = new RSVP(new KTConferenceUser(Convert.ToInt32(row["userID"])), row["invitationType"].ToString());

                string confirmationCode = rsvp.ConfirmationCode;
            }
        }

        public int SendInvitations(object sender, int inviteClass, bool testingOnly)
        {
            DataTable tbl = WebDataUtility.Instance.webAppTable("sp_GetUnsentInvitationsInClass",
        new GenericCmdParameter[] { 
                        new GenericCmdParameter("@conferenceID", this.ID), 
                        new GenericCmdParameter("@inviteClass", inviteClass) });
            int i = 1;
            DateTime deadline = (inviteClass == 1 ? this.PrimaryRegistrationClosed : (inviteClass == 2 ? this.LateRegistrationClosed : this.NoChangeDate));

            foreach (DataRow row in tbl.Rows)
            {
                try
                {
                    KTConferenceUser user = new KTConferenceUser(testingOnly ? System.Configuration.ConfigurationManager.AppSettings["sysadmin"]  : Convert.ToString(row["userEmail"]));

                    SendInvitation((Control)sender, user, deadline);
                    ExceptionUtility.LogException(new Exception("SUCCESS: Invite Reminder sent to " +
                        user.FullName.ToUpper() + ". Total Emails sent: " + (++i)), "ConferenceObjects.ConferenceLibrary.Conference.SendReminderEmails(int inviteClass)");
                }
                catch (Exception e)
                {
                    ExceptionUtility.LogException(e, "ConferenceObjects.ConferenceLibrary.Conference.SendReminderEmails(int inviteClass)");

                }
            }
            return i;

        }

        public int SendReminderEmails(object sender, int inviteClass, bool testingOnly)
        {
            DataTable tbl = WebDataUtility.Instance.webAppTable("sp_GetUnregisteredInviteesInClass",
                    new GenericCmdParameter[] { 
                        new GenericCmdParameter("@conferenceID", this.ID), 
                        new GenericCmdParameter("@inviteClass", inviteClass) });
            int i = 1;
            DateTime deadline = (inviteClass == 1 ? this.PrimaryRegistrationClosed : (inviteClass == 2 ? this.LateRegistrationClosed : this.NoChangeDate));
            Control context = (Control)sender;
            foreach (DataRow row in tbl.Rows)
            {
                try
                {
                    KTConferenceUser user = new KTConferenceUser(testingOnly ? System.Configuration.ConfigurationManager.AppSettings["sysadmin"]  : Convert.ToString(row["userEmail"]));

                    SendInvitationReminder(context, user);
                    
                    ExceptionUtility.LogException(new Exception("SUCCESS: Invite Reminder sent to " +
                        user.FullName.ToUpper() + ". Total Emails sent: " + (++i)), "ConferenceObjects.ConferenceLibrary.Conference.SendReminderEmails(int inviteClass)");
                }
                catch (Exception e)
                {
                    ExceptionUtility.LogException(e, "ConferenceObjects.ConferenceLibrary.Conference.SendReminderEmails(int inviteClass)");
                    
                }
            }
            return i;
        }

        public void SendConfirmationEmail(Page page, KTConferenceUser user, string admin, string schedulePDF)
        {

            string url = page.Request.Url.GetLeftPart(UriPartial.Authority)
                            + page.Request.ApplicationPath + "/ConfirmationEmailBody.aspx?user=" + user.Email;

            try
            {
                System.Net.HttpWebRequest req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
                req.Credentials = System.Net.CredentialCache.DefaultCredentials;
                System.Net.HttpWebResponse res = (System.Net.HttpWebResponse)req.GetResponse();


                StringBuilder sb = new StringBuilder();
                using (StreamReader sr = new StreamReader(res.GetResponseStream()))
                {
                    string html = sr.ReadToEnd();
    
                    String line;
                    // Read and display lines from the file until the end of 
                    // the file is reached.
                    while ((line = sr.ReadLine()) != null)
                    {
                        sb.AppendLine(line);
                    }

                    DataTable guestSpeakers = WebDataUtility.Instance.webAppTable("sp_GetGuestSpeakers",
                        new GenericCmdParameter[] { new GenericCmdParameter("@conferenceID", ID) });
                    List<LinkedResource> imgList = new List<LinkedResource>();
                    int i = 0;
                    foreach (DataRow row in guestSpeakers.Rows)
                    {
                        string resourceName = DBNullable.ToString(row["imageName"]).Replace(" ",string.Empty);
                        string speakeName = string.Format("{0} {1}",
                            DBNullable.ToString(row["userFirstName"]), DBNullable.ToString(row["userLastName"]));
                        string title = DBNullable.ToString(row["userTitle"]);

                        Bitmap b = new Bitmap((Image)LibraryResources.ResourceManager.GetObject(resourceName));
                        ImageConverter ic = new ImageConverter();
                        Byte[] by = (Byte[])ic.ConvertTo(b, typeof(Byte[]));
                        MemoryStream ms = new MemoryStream(by);
                        LinkedResource img = new LinkedResource(ms, "image/jpeg");
                        img.ContentId = resourceName;
                        html = html.Replace("{" + i++ + "}", string.Format("<a href='http://productivity/EngineeringConferenceRegistration/Balloon.aspx?type=execspeaker&id={3}'><img style='border:none' src=cid:{0} /></a><br><b>{1}</b><br>{2}", 
                            resourceName, speakeName, title, DBNullable.ToString(row["userID"])));
                        imgList.Add(img);

                    }
                    Bitmap header = new Bitmap(LibraryResources.header);
                    ImageConverter icHeader = new ImageConverter();
                    Byte[] headerByte = (Byte[])icHeader.ConvertTo(header, typeof(Byte[]));
                    MemoryStream msHeader = new MemoryStream(headerByte);
                    LinkedResource lrHeader = new LinkedResource(msHeader, "image/jpeg");
                    lrHeader.ContentId = "header";
                    html = html.Replace("{imgheader}", "<img src=cid:header />");


                    Bitmap map = new Bitmap(LibraryResources.map);
                    ImageConverter icMap = new ImageConverter();
                    Byte[] mapByte = (Byte[])icMap.ConvertTo(map, typeof(Byte[]));
                    MemoryStream msMap = new MemoryStream(mapByte);
                    LinkedResource lrMap = new LinkedResource(msMap, "image/jpeg");
                    lrMap.ContentId = "map";
                    html = html.Replace("{map}", "<img src=cid:map />");

                    Bitmap apple = new Bitmap(LibraryResources.apple);
                    ImageConverter icApple = new ImageConverter();
                    Byte[] appleByte = (Byte[])icApple.ConvertTo(apple, typeof(Byte[]));
                    MemoryStream msApple = new MemoryStream(appleByte);
                    LinkedResource lrApple = new LinkedResource(msApple, "image/jpeg");
                    lrApple.ContentId = "apple";
                    html = html.Replace("{apple}", "<img src=cid:apple />");

                    Bitmap google = new Bitmap(LibraryResources.google);
                    ImageConverter icGoogle = new ImageConverter();
                    Byte[] googleByte = (Byte[])icGoogle.ConvertTo(google, typeof(Byte[]));
                    MemoryStream msGoogle = new MemoryStream(googleByte);
                    LinkedResource lrGoogle = new LinkedResource(msGoogle, "image/jpeg");
                    lrGoogle.ContentId = "google";
                    html = html.Replace("{google}", "<img src=cid:google />");

                    Bitmap web = new Bitmap(LibraryResources.web);
                    ImageConverter icWeb = new ImageConverter();
                    Byte[] webByte = (Byte[])icWeb.ConvertTo(web, typeof(Byte[]));
                    MemoryStream msWeb = new MemoryStream(webByte);
                    LinkedResource lrWeb = new LinkedResource(msWeb, "image/jpeg");
                    lrWeb.ContentId = "web";
                    html = html.Replace("{web}", "<img src=cid:web />");


                    Bitmap security = new Bitmap(LibraryResources.imgsecuritymarking);
                    ImageConverter icsecurity = new ImageConverter();
                    Byte[] securityByte = (Byte[])icsecurity.ConvertTo(security, typeof(Byte[]));
                    MemoryStream mssecurity = new MemoryStream(securityByte);
                    LinkedResource lrsecurity = new LinkedResource(mssecurity, "image/jpeg");
                    lrsecurity.ContentId = "imgsecurity";
                    html = html.Replace("{imgsecuritymarking}", "<img src=cid:imgsecurity />");

                    AlternateView view = AlternateView.CreateAlternateViewFromString(html, null, "text/html");

                    view.LinkedResources.Add(lrHeader);
                    view.LinkedResources.Add(lrMap);
                    view.LinkedResources.Add(lrsecurity);

                    Email email = new Email(user.Email, sb.ToString(), "2015 Engineering Conference Confirmation");

                    email.AddAlternativeView(view);
                    email.AddAttachment(schedulePDF);

                    if (admin.Length > 0)
                        email.AddCCAddressee(admin);

                    email.AddBccAddressee(System.Configuration.ConfigurationManager.AppSettings["sysadmin"] );
                    email.Send();

                    ExceptionUtility.LogException(new Exception("SUCCESS: Confirmatiion sent to " +
                        user.FullName.ToUpper()), "ConferenceObjects.ConferenceLibrary.Conference.SendConfirmationEmail(Page page, KTConferenceUser user, string schedulePDF)");
                    object retval = null;
                    WebDataUtility.Instance.webAppCmd("sp_UpdateConfirmationLog",
                        new GenericCmdParameter[] { new GenericCmdParameter("@userID", user.UserID), new GenericCmdParameter("@conferenceID", ID) }, ref retval);

                }
            }
            catch (Exception ex)
            {
                ExceptionUtility.LogException(new Exception("FAILED: Invite sent to " +
                    user.FullName.ToUpper()), "ConferenceObjects.ConferenceLibrary.Conference.SendStaffInvitationEmails(Page page, KTConferenceUser user, string schedulePDF)");
                ExceptionUtility.LogException(ex, "ConferenceObjects.ConferenceLibrary.Conference.SendStaffInvitationEmails(Page page, KTConferenceUser user, string schedulePDF)");
            }

            
        }

        public void SendGenericConferenceEmail(Page page, RSVP rsvp, string passedUrl, string emailSubject, string fileToAttach, bool highImportance, bool testingOnly)
        {
            if (rsvp == null)
                return;
            if (rsvp.ConfirmationCode.Equals(string.Empty))
                return;

            string url = ConfigurationManager.AppSettings["EmailTemplatePath"] + passedUrl + "?email=" + rsvp.User.Email;
 
            try
            {
                System.Net.HttpWebRequest req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
                req.Credentials = System.Net.CredentialCache.DefaultCredentials;
                System.Net.HttpWebResponse res = (System.Net.HttpWebResponse)req.GetResponse();


                StringBuilder sb = new StringBuilder();
                using (StreamReader sr = new StreamReader(res.GetResponseStream()))
                {
                    string html = sr.ReadToEnd();

                    String line;
                    // Read and display lines from the file until the end of 
                    // the file is reached.
                    while ((line = sr.ReadLine()) != null)
                    {
                        sb.AppendLine(line);
                    }

                    Bitmap header = new Bitmap(LibraryResources.header);
                    ImageConverter icHeader = new ImageConverter();
                    Byte[] headerByte = (Byte[])icHeader.ConvertTo(header, typeof(Byte[]));
                    MemoryStream msHeader = new MemoryStream(headerByte);
                    LinkedResource lrHeader = new LinkedResource(msHeader, "image/jpeg");
                    lrHeader.ContentId = "header";
                    html = html.Replace("{header}", "<img src=cid:header />");

                    Bitmap security = new Bitmap(LibraryResources.imgsecuritymarking);
                    ImageConverter icsecurity = new ImageConverter();
                    Byte[] securityByte = (Byte[])icsecurity.ConvertTo(security, typeof(Byte[]));
                    MemoryStream mssecurity = new MemoryStream(securityByte);
                    LinkedResource lrsecurity = new LinkedResource(mssecurity, "image/jpeg");
                    lrsecurity.ContentId = "imgsecurity";
                    html = html.Replace("{imgsecuritymarking}", "<img src=cid:imgsecurity />");

                    AlternateView view = AlternateView.CreateAlternateViewFromString(html, null, "text/html");

                    view.LinkedResources.Add(lrHeader);
                    view.LinkedResources.Add(lrsecurity);
                    Email email = new Email((testingOnly?System.Configuration.ConfigurationManager.AppSettings["sysadmin"] :rsvp.User.Email), sb.ToString(), emailSubject);

                    email.AddAlternativeView(view);

                    if (rsvp.Admin != null)
                    {
                        if (!rsvp.Admin.Email.Equals(String.Empty))
                            email.AddCCAddressee(rsvp.Admin.Email);
                    }

                    if (fileToAttach != string.Empty)
                    {
                        email.AddAttachment(fileToAttach);
                    }
                    if(highImportance)
                        email.SetPriority(MailPriority.High);

                    email.AddBccAddressee(System.Configuration.ConfigurationManager.AppSettings["sysadmin"] );
                    email.Send();

                    ExceptionUtility.LogException(new Exception("SUCCESS: Email sent to " +
                        rsvp.User.FullName.ToUpper()), "ConferenceObjects.ConferenceLibrary.Conference.SendGenericConferenceEmail(Page page, RSVP rsvp, string passedUrl, string emailSubject)");
                    object retval = null;

                  
                    //WebDataUtility.Instance.webAppCmd("sp_UpdateConfirmationLog",
                    //    new GenericCmdParameter[] { new GenericCmdParameter("@userID", rsvp.User.UserID), new GenericCmdParameter("@conferenceID", ID) }, ref retval);

                }
            }
            catch (Exception ex)
            {
                ExceptionUtility.LogException(new Exception("FAILED: Email not sent to " +
                    rsvp.User.FullName.ToUpper()), "ConferenceObjects.ConferenceLibrary.Conference.SendGenericConferenceEmail(Page page, RSVP rsvp, string passedUrl, string emailSubject)");
                ExceptionUtility.LogException(ex, "ConferenceObjects.ConferenceLibrary.Conference.SendGenericConferenceEmail(Page page, RSVP rsvp, string passedUrl, string emailSubject)");
            }


        }
  
        public void SendMobileAppEmail(Page page, RSVP rsvp)
        {
            if (rsvp == null)
                return;
            if (rsvp.ConfirmationCode.Equals(string.Empty))
                return;

            string url = page.Request.Url.GetLeftPart(UriPartial.Authority)
                            + page.Request.ApplicationPath + "/MobileAppEmail.aspx?confCode=" + rsvp.ConfirmationCode;

            try
            {
                System.Net.HttpWebRequest req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
                req.Credentials = System.Net.CredentialCache.DefaultCredentials;
                System.Net.HttpWebResponse res = (System.Net.HttpWebResponse)req.GetResponse();


                StringBuilder sb = new StringBuilder();
                using (StreamReader sr = new StreamReader(res.GetResponseStream()))
                {
                    string html = sr.ReadToEnd();

                    String line;
                    // Read and display lines from the file until the end of 
                    // the file is reached.
                    while ((line = sr.ReadLine()) != null)
                    {
                        sb.AppendLine(line);
                    }

                    Bitmap header = new Bitmap(LibraryResources.header);
                    ImageConverter icHeader = new ImageConverter();
                    Byte[] headerByte = (Byte[])icHeader.ConvertTo(header, typeof(Byte[]));
                    MemoryStream msHeader = new MemoryStream(headerByte);
                    LinkedResource lrHeader = new LinkedResource(msHeader, "image/jpeg");
                    lrHeader.ContentId = "header";
                    html = html.Replace("{header}", "<img src=cid:header />");


                    Bitmap map = new Bitmap(LibraryResources.map);
                    ImageConverter icMap = new ImageConverter();
                    Byte[] mapByte = (Byte[])icMap.ConvertTo(map, typeof(Byte[]));
                    MemoryStream msMap = new MemoryStream(mapByte);
                    LinkedResource lrMap = new LinkedResource(msMap, "image/jpeg");
                    lrMap.ContentId = "map";
                    html = html.Replace("{map}", "<img src=cid:map />");

                    Bitmap apple = new Bitmap(LibraryResources.appStore);
                    ImageConverter icApple = new ImageConverter();
                    Byte[] appleByte = (Byte[])icApple.ConvertTo(apple, typeof(Byte[]));
                    MemoryStream msApple = new MemoryStream(appleByte);
                    LinkedResource lrApple = new LinkedResource(msApple, "image/jpeg");
                    lrApple.ContentId = "apple";
                    html = html.Replace("{apple}", "<img src=cid:apple />");

                    Bitmap google = new Bitmap(LibraryResources.googlePlayStore);
                    ImageConverter icGoogle = new ImageConverter();
                    Byte[] googleByte = (Byte[])icGoogle.ConvertTo(google, typeof(Byte[]));
                    MemoryStream msGoogle = new MemoryStream(googleByte);
                    LinkedResource lrGoogle = new LinkedResource(msGoogle, "image/jpeg");
                    lrGoogle.ContentId = "google";
                    html = html.Replace("{google}", "<img src=cid:google />");



                    Bitmap security = new Bitmap(LibraryResources.imgsecuritymarking);
                    ImageConverter icsecurity = new ImageConverter();
                    Byte[] securityByte = (Byte[])icsecurity.ConvertTo(security, typeof(Byte[]));
                    MemoryStream mssecurity = new MemoryStream(securityByte);
                    LinkedResource lrsecurity = new LinkedResource(mssecurity, "image/jpeg");
                    lrsecurity.ContentId = "imgsecurity";
                    html = html.Replace("{imgsecuritymarking}", "<img src=cid:imgsecurity />");

                    
                    
                    
                    
                    
                    AlternateView view = AlternateView.CreateAlternateViewFromString(html, null, "text/html");




                    view.LinkedResources.Add(lrHeader);
                    view.LinkedResources.Add(lrMap);

                    view.LinkedResources.Add(lrApple);
                    view.LinkedResources.Add(lrGoogle);
                    view.LinkedResources.Add(lrsecurity);

                    Email email = new Email(rsvp.User.Email , sb.ToString(), "2015 Engineering Conference Mobile App!");

                    email.AddAlternativeView(view);

                    if (rsvp.Admin != null)
                    {
                        if(!rsvp.Admin.Email.Equals(String.Empty))
                            email.AddCCAddressee(rsvp.Admin.Email);
                    }
                    email.AddBccAddressee(System.Configuration.ConfigurationManager.AppSettings["sysadmin"] );
                    email.SetPriority(MailPriority.High);
                    email.Send();

                    ExceptionUtility.LogException(new Exception("SUCCESS: MobileAppEmail sent to " +
                        rsvp.User.FullName.ToUpper()), "ConferenceObjects.ConferenceLibrary.Conference.SendMobileAppEmail(Page page, RSVP rsvp)");
                    object retval = null;
                    //WebDataUtility.Instance.webAppCmd("sp_UpdateConfirmationLog",
                    //    new GenericCmdParameter[] { new GenericCmdParameter("@userID", rsvp.User.UserID), new GenericCmdParameter("@conferenceID", ID) }, ref retval);

                }
            }
            catch (Exception ex)
            {
                ExceptionUtility.LogException(new Exception("FAILED: Invite sent to " +
                    rsvp.User.FullName.ToUpper()), "ConferenceObjects.ConferenceLibrary.Conference.SendStaffInvitationEmails(Page page, KTConferenceUser user, string schedulePDF)");
                ExceptionUtility.LogException(ex, "ConferenceObjects.ConferenceLibrary.Conference.SendStaffInvitationEmails(Page page, KTConferenceUser user, string schedulePDF)");
            }


        }

        public void SendInvitation(Control context, KTConferenceUser user, DateTime deadline)
        {
            AlternateView view = null;
            string html = string.Empty;

            string url = ConfigurationManager.AppSettings["EmailTemplatePath"] + "Invitation.aspx?user=" + user.Email;
            url = new Uri(context.Page.Request.Url, context.ResolveClientUrl(url)).AbsoluteUri;

            try
            {
                System.Net.HttpWebRequest req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
                req.Credentials = System.Net.CredentialCache.DefaultCredentials;
                System.Net.HttpWebResponse res = (System.Net.HttpWebResponse)req.GetResponse();
                StreamReader sr = new StreamReader(res.GetResponseStream());
                html = sr.ReadToEnd();

                List<LinkedResource> imgList = new List<LinkedResource>();

                Bitmap header = new Bitmap(LibraryResources.imgheader);
                ImageConverter icHeader = new ImageConverter();
                Byte[] headerByte = (Byte[])icHeader.ConvertTo(header, typeof(Byte[]));
                MemoryStream msHeader = new MemoryStream(headerByte);
                LinkedResource lrHeader = new LinkedResource(msHeader, "image/jpeg");
                lrHeader.ContentId = "imgheader";
                html = html.Replace("{imgheader}", "<img src=cid:imgheader />");

                Bitmap footer = new Bitmap(LibraryResources.imgfooter);
                ImageConverter icfooter = new ImageConverter();
                Byte[] footerByte = (Byte[])icfooter.ConvertTo(footer, typeof(Byte[]));
                MemoryStream msfooter = new MemoryStream(footerByte);
                LinkedResource lrfooter = new LinkedResource(msfooter, "image/jpeg");
                lrfooter.ContentId = "imgfooter";
                html = html.Replace("{imgfooter}", "<img src=cid:imgfooter />");

                Bitmap security = new Bitmap(LibraryResources.imgsecuritymarking);
                ImageConverter icsecurity = new ImageConverter();
                Byte[] securityByte = (Byte[])icsecurity.ConvertTo(security, typeof(Byte[]));
                MemoryStream mssecurity = new MemoryStream(securityByte);
                LinkedResource lrsecurity = new LinkedResource(mssecurity, "image/jpeg");
                lrsecurity.ContentId = "imgsecurity";
                html = html.Replace("{imgsecuritymarking}", "<img src=cid:imgsecurity />");

                
                view = AlternateView.CreateAlternateViewFromString(html, null, "text/html");

                view.LinkedResources.Add(lrHeader);
                view.LinkedResources.Add(lrfooter);
                view.LinkedResources.Add(lrsecurity);

            }
            catch (Exception e)
            {
                ExceptionUtility.LogException(e, "ConferenceObjects.ConferenceLibrary.Conference.SendInvitation(" + user.Email + ")");

            }
            Email email = new Email(user.Email, html, "Invitation to " + this.ConferenceTitle);

            if(view != null)    
                email.AddAlternativeView(view);

            email.SendZainEmail();
            try
            {
                UpdateInviteFlag(user.UserID);
            }
            catch (Exception ex)
            {
            }

        }

        public void SendConfirmationEmailBatch(object sender, bool testingOnly)
        {
            DataTable confirmationBatch = WebDataUtility.Instance.webAppTable("sp_GetRSVPConfirmationBatch",
                new GenericCmdParameter[] { new GenericCmdParameter("@conferenceID", ID) });

            foreach (DataRow row in confirmationBatch.Rows)
            {
           //     RSVP rsvp = new RSVP(new KTConferenceUser(testingOnly ? System.Configuration.ConfigurationManager.AppSettings["sysadmin"]  : row["userEmail"].ToString()), "Guest");
                RSVP rsvp = new RSVP(new KTConferenceUser(row["userEmail"].ToString()), "Guest");

                AlternateView view = null;
                if (rsvp.IsValid)
                {

                    string html = string.Empty;
                    Control context = (Control)sender;

                    string url = ConfigurationManager.AppSettings["EmailTemplatePath"] + "ConferenceConfirmation.aspx?user=" + rsvp.User.Email;
                    url = new Uri(context.Page.Request.Url, context.ResolveClientUrl(url)).AbsoluteUri;
                    
                    try
                    {
                        HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                        req.Credentials = CredentialCache.DefaultCredentials;
                        HttpWebResponse res = (HttpWebResponse)req.GetResponse();
                        StreamReader sr = new StreamReader(res.GetResponseStream());
                        html = sr.ReadToEnd();

                        List<LinkedResource> imgList = new List<LinkedResource>();

                        DataTable guestSpeakers = WebDataUtility.Instance.webAppTable("sp_GetGuestSpeakers",
                            new GenericCmdParameter[] { new GenericCmdParameter("@conferenceID", ID) });
                        int i = 0;
                        foreach (DataRow guestrow in guestSpeakers.Rows)
                        {
                            string resourceName = DBNullable.ToString(guestrow["imageName"]).Replace(" ", string.Empty).ToLower();
                            string speakeName = string.Format("{0} {1}",
                                DBNullable.ToString(guestrow["userFirstName"]), DBNullable.ToString(guestrow["userLastName"]));
                            string title = DBNullable.ToString(guestrow["userTitle"]);

                            Bitmap b = new Bitmap((Image)LibraryResources.ResourceManager.GetObject(resourceName));
                            ImageConverter ic = new ImageConverter();
                            Byte[] by = (Byte[])ic.ConvertTo(b, typeof(Byte[]));
                            MemoryStream ms = new MemoryStream(by);
                            LinkedResource img = new LinkedResource(ms, "image/jpeg");
                            img.ContentId = resourceName;
                            html = html.Replace("{" + i++ + "}", string.Format("<a href='http://productivity/EngineeringConferenceRegistration/Balloon.aspx?type=execspeaker&id={3}'><img style='border:none' src=cid:{0} /></a><br><b>{1}</b><br>{2}",
                                resourceName, speakeName, title, DBNullable.ToString(guestrow["userID"])));
                            imgList.Add(img);

                        }

                        Bitmap header = new Bitmap(LibraryResources.imgheader);
                        ImageConverter icHeader = new ImageConverter();
                        Byte[] headerByte = (Byte[])icHeader.ConvertTo(header, typeof(Byte[]));
                        MemoryStream msHeader = new MemoryStream(headerByte);
                        LinkedResource lrHeader = new LinkedResource(msHeader, "image/jpeg");
                        lrHeader.ContentId = "imgheader";
                        html = html.Replace("{imgheader}", "<img src=cid:imgheader />");

                        Bitmap footer = new Bitmap(LibraryResources.imgfooter);
                        ImageConverter icfooter = new ImageConverter();
                        Byte[] footerByte = (Byte[])icfooter.ConvertTo(footer, typeof(Byte[]));
                        MemoryStream msfooter = new MemoryStream(footerByte);
                        LinkedResource lrfooter = new LinkedResource(msfooter, "image/jpeg");
                        lrfooter.ContentId = "imgfooter";
                        html = html.Replace("{imgfooter}", "<img src=cid:imgfooter />");

                        Bitmap security = new Bitmap(LibraryResources.imgsecuritymarking);
                        ImageConverter icsecurity = new ImageConverter();
                        Byte[] securityByte = (Byte[])icsecurity.ConvertTo(security, typeof(Byte[]));
                        MemoryStream mssecurity = new MemoryStream(securityByte);
                        LinkedResource lrsecurity = new LinkedResource(mssecurity, "image/jpeg");
                        lrsecurity.ContentId = "imgsecurity";
                        html = html.Replace("{imgsecuritymarking}", "<img src=cid:imgsecurity />");

                        Bitmap map = new Bitmap(LibraryResources.map);
                        ImageConverter icMap = new ImageConverter();
                        Byte[] mapByte = (Byte[])icMap.ConvertTo(map, typeof(Byte[]));
                        MemoryStream msMap = new MemoryStream(mapByte);
                        LinkedResource lrMap = new LinkedResource(msMap, "image/jpeg");
                        lrMap.ContentId = "map";
                        html = html.Replace("{map}", "<img src=cid:map />");

                        Bitmap apple = new Bitmap(LibraryResources.apple);
                        ImageConverter icApple = new ImageConverter();
                        Byte[] appleByte = (Byte[])icApple.ConvertTo(apple, typeof(Byte[]));
                        MemoryStream msApple = new MemoryStream(appleByte);
                        LinkedResource lrApple = new LinkedResource(msApple, "image/jpeg");
                        lrApple.ContentId = "apple";
                        html = html.Replace("{apple}", "<img src=cid:apple />");

                        Bitmap google = new Bitmap(LibraryResources.google);
                        ImageConverter icGoogle = new ImageConverter();
                        Byte[] googleByte = (Byte[])icGoogle.ConvertTo(google, typeof(Byte[]));
                        MemoryStream msGoogle = new MemoryStream(googleByte);
                        LinkedResource lrGoogle = new LinkedResource(msGoogle, "image/jpeg");
                        lrGoogle.ContentId = "google";
                        html = html.Replace("{google}", "<img src=cid:google />");

                        Bitmap web = new Bitmap(LibraryResources.web);
                        ImageConverter icWeb = new ImageConverter();
                        Byte[] webByte = (Byte[])icWeb.ConvertTo(web, typeof(Byte[]));
                        MemoryStream msWeb = new MemoryStream(webByte);
                        LinkedResource lrWeb = new LinkedResource(msWeb, "image/jpeg");
                        lrWeb.ContentId = "web";
                        html = html.Replace("{web}", "<img src=cid:web />");


                        view = AlternateView.CreateAlternateViewFromString(html, null, "text/html");

                        view.LinkedResources.Add(lrHeader);
                        view.LinkedResources.Add(lrfooter);
                        view.LinkedResources.Add(lrsecurity);
                        view.LinkedResources.Add(lrMap);
                        view.LinkedResources.Add(lrApple);
                        view.LinkedResources.Add(lrGoogle);
                        view.LinkedResources.Add(lrWeb);

                        foreach (LinkedResource lr in imgList)
                            view.LinkedResources.Add(lr);


                    }
                    catch (Exception ex)
                    {
                        ExceptionUtility.LogException(ex, "ConferenceObjects.ConferenceLibrary.Conference.SendConfirmationEmail(" + rsvp.User.Email + ")");
                    }

                    Email email = new Email(testingOnly? System.Configuration.ConfigurationManager.AppSettings["sysadmin"]  : rsvp.User.Email, html, this.ConferenceTitle + " Confirmation");

                  
                    if (view != null)
                        email.AddAlternativeView(view);

                    email.SetPriority(MailPriority.High);
                    email.SendZainEmail();

                    if(!testingOnly)
                        rsvp.UpdateConfirmationFlag();

                //  rsvp.SendEmailConfirmation(view, html, rsvp.IsNew);
                    
                }
                try
                {
                 //   UpdateConfirmationEmailFlag(rsvp.User.UserID, rsvp.ConfirmationCode);
                }
                catch (Exception ex)
                {
                }
            }

        } 
            
        void UpdateConfirmationEmailFlag(int userID, string confirmationCode)
        {
            object retval = 0;
            WebDataUtility.Instance.webAppCmd("sp_UpdateRSVPConfirmationFlag",
                new GenericCmdParameter[] { 
                    new GenericCmdParameter("@userID", userID), 
                    new GenericCmdParameter("@confirmation", confirmationCode) }, ref retval);
        }

        private void UpdateInviteFlag(int userID)
        {
            object retval = 0;
            WebDataUtility.Instance.webAppCmd("sp_UpdateInviteFlag",
                new GenericCmdParameter[] { new GenericCmdParameter("userID", userID), new GenericCmdParameter("@conferenceID", ID) }, ref retval);
        }

        public void SendInvitationReminder(Control context, KTConferenceUser user)
        {
            AlternateView view = null;
            string html = string.Empty;

            string url = ConfigurationManager.AppSettings["EmailTemplatePath"] + "InviteReminder.aspx?user=" + user.Email;
            url = new Uri(context.Page.Request.Url, context.ResolveClientUrl(url)).AbsoluteUri;

            try
            {
                System.Net.HttpWebRequest req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
                req.Credentials = System.Net.CredentialCache.DefaultCredentials;
                System.Net.HttpWebResponse res = (System.Net.HttpWebResponse)req.GetResponse();
                StreamReader sr = new StreamReader(res.GetResponseStream());
                html = sr.ReadToEnd();

                List<LinkedResource> imgList = new List<LinkedResource>();

                Bitmap header = new Bitmap(LibraryResources.imgheader);
                ImageConverter icHeader = new ImageConverter();
                Byte[] headerByte = (Byte[])icHeader.ConvertTo(header, typeof(Byte[]));
                MemoryStream msHeader = new MemoryStream(headerByte);
                LinkedResource lrHeader = new LinkedResource(msHeader, "image/jpeg");
                lrHeader.ContentId = "imgheader";
                html = html.Replace("{imgheader}", "<img src=cid:imgheader />");

                Bitmap footer = new Bitmap(LibraryResources.imgfooter);
                ImageConverter icfooter = new ImageConverter();
                Byte[] footerByte = (Byte[])icfooter.ConvertTo(footer, typeof(Byte[]));
                MemoryStream msfooter = new MemoryStream(footerByte);
                LinkedResource lrfooter = new LinkedResource(msfooter, "image/jpeg");
                lrfooter.ContentId = "imgfooter";
                html = html.Replace("{imgfooter}", "<img src=cid:imgfooter />");

                Bitmap security = new Bitmap(LibraryResources.imgsecuritymarking);
                ImageConverter icsecurity = new ImageConverter();
                Byte[] securityByte = (Byte[])icsecurity.ConvertTo(security, typeof(Byte[]));
                MemoryStream mssecurity = new MemoryStream(securityByte);
                LinkedResource lrsecurity = new LinkedResource(mssecurity, "image/jpeg");
                lrsecurity.ContentId = "imgsecurity";
                html = html.Replace("{imgsecuritymarking}", "<img src=cid:imgsecurity />");


                view = AlternateView.CreateAlternateViewFromString(html, null, "text/html");

                view.LinkedResources.Add(lrHeader);
                view.LinkedResources.Add(lrfooter);
                view.LinkedResources.Add(lrsecurity);
            }
            catch (Exception e)
            {
                ExceptionUtility.LogException(e, "ConferenceObjects.ConferenceLibrary.Conference.SendInvitationReminder(" + user.Email + ")");

            }
            Email email = new Email(user.Email, html, "Invitation to " + this.ConferenceTitle);
            if (view != null)
                email.AddAlternativeView(view);

            email.Send();

        }

        public void SendStaffInvitationEmails(bool testingOnly)
        {
            DataTable tbl = WebDataUtility.Instance.webAppTable("sp_GetKTStaff",
            new GenericCmdParameter[] { new GenericCmdParameter("@conferenceID", Conference.Instance.ID) });

            int i = 0;
            foreach (DataRow row in tbl.Rows)
            {
                try
                {
                    KTConferenceUser user = new KTConferenceUser((testingOnly?System.Configuration.ConfigurationManager.AppSettings["sysadmin"] :Convert.ToString(row["userEmail"])));

                    string url = "http://productivity/EngineeringConference_Administration/RegisterStaff.aspx?user=" + DBNullable.ToString(row["userLogin"]);
                    SendStaffInvitation(user,url);
                    ExceptionUtility.LogException(new Exception("SUCCESS: Invite sent to " +
                        user.FullName.ToUpper() + ". Total Emails sent: " + (++i)), "ConferenceObjects.ConferenceLibrary.Conference.SendStaffInvitationEmails()");
                }
                catch (Exception e)
                {
                    ExceptionUtility.LogException(e, "ConferenceObjects.ConferenceLibrary.Conference.SendStaffInvitationEmails()");

                }
            }
        }

        public void SendStaffInvitation(KTConferenceUser user, string regURL)
        {

            //Add basic information.

            StringBuilder sb = new StringBuilder();
            using (StreamReader sr = new StreamReader(HttpContext.Current.Server.MapPath(@"Files/EmailHeaderSnippet.txt")))
            {
                String line;
                // Read and display lines from the file until the end of 
                // the file is reached.
                while ((line = sr.ReadLine()) != null)
                {
                    sb.AppendLine(line);
                }
            }


            string richText = "<div style='width:680px; padding:10px'>" + user.FirstName + ", " +
                 "<p>The registration site for KT Event Staff working the 2015 Engineering Conference is now available. " +
                "While we're striving to have rooms for each member of the staff at the conference venue, we may need some staff memebers to stay at a nearby hotel.</p>" +
                "<p>You can register at the following webpage: <a href='" + regURL +
                "'>Engineering Conference Staff Registration Site</a>. If you have difficulty registering, please contact <a href='mailto:Daniel.Stauffer@kla-tencor.com'>Dan Stauffer, Corp PLC.</a></p>" +
                "<p>Dan will hold a Staff Team Meeting shortly - please watch for that meeting invite.</p>" +
                "<p><u>If you are unable to staff the conference, please please contact <a href='mailto:Daniel.Stauffer@kla-tencor.com'>Dan Stauffer, Corp PLC.</a></u></p>" +
                "<p>Thank you for volunteering your time to staff the 2015 Engineering Conference!</p>" +

                "<p>Regards,<br>2015 Engineering Conference Steering Team</p></div>";
            sb.AppendLine(richText);
            sb.AppendLine("</body></html>");
            Email email = new Email(user.Email, sb.ToString(), "Staff Registration for the  " + DateTime.Now.ToString("yyyy") + " " + this.ConferenceTitle);
            email.AddBccAddressee(System.Configuration.ConfigurationManager.AppSettings["sysadmin"] );
            email.SetPriority(MailPriority.High);
            email.Send();
        }

        public DataTable InviteTypes()
        {
            return WebDataUtility.Instance.webAppTable("tbl_InviteTypes");
        }

        public void AddInvitee(KTConferenceUser user, string divisionText, string inviteType, bool isExec)
        {
            object retval = null;

            WebDataUtility.Instance.webAppCmd("sp_LoadNewInviteee", new GenericCmdParameter[] {
                new GenericCmdParameter("@userID", user.UserID),
                new GenericCmdParameter("@conferenceID", this.ID),
                new GenericCmdParameter("@userEmail", user.Email),
                new GenericCmdParameter("@divisionText", divisionText),
                new GenericCmdParameter("@inviteType", inviteType),
                new GenericCmdParameter("@isExec", isExec),
                new GenericCmdParameter("@inviteClass", DateTime.Now < this.PrimaryRegistrationClosed?1:2),
                new GenericCmdParameter("@inviteSent", false),
                new GenericCmdParameter("@declined", false)}, ref retval);
            
        }
        
        }

    public class ConferenceEvent
    {
        private int _id = 0;
        private DateTime _start;
        private DateTime _stop;
        private string _location = string.Empty;
        private string _type = string.Empty;
        private string _title = string.Empty;
        private int _parentID = 0;
        private string _media = string.Empty;
        private bool _isPublic = false;
        private int _maxRequests = 1;
        private DateTime _lastUpdated = DateTime.Now;
        private int _venueID = 1;
        
        public int ID
        {
            get { return _id; }
        }

        public DateTime Start
        {
            get { return _start; }
            set { _start = value; }
        }

        public DateTime Stop
        {
            get { return _stop; }
            set { _stop = value; }
        }

        public string Location
        {
            get { return _location; }
            set { _location = value; }
        }

        public string Type
        {
            get { return _type; }
            set { _type = value; }
        }

        public string Title
        {
            get { return _title; }
            set { _title = value; }
        }

        public int ParentID
        {
            get { return _parentID; }
            set { _parentID = value; }
        }

        public string Media
        {
            get { return _media; }
            set { _media = value; }
        }

        public int MaxRequests
        {
            get { return _maxRequests; }
            set { _maxRequests = value; }
        }

        public bool IsPublic
        {
            get { return _isPublic; }
            set { _isPublic = value; }
        }

        public int VenueID
        {
            get { return _venueID; }

        }

        public ConferenceEvent(DateTime start, DateTime stop, string location, 
                string type, string title, int parentID, string media,  int maxRequests, bool isPublic)
        {
            _start = start;
            _stop = stop;
            _location = location;
            _type = type;
            _title = title;
            _parentID = parentID;
            _media = media;
            _maxRequests = maxRequests;
            _isPublic = isPublic;
            if (!validateDates(start,stop))
                throw new ArgumentException("Dates are invalid. Ensure that stop time occurs after start time or that the event's time fall within the selected parent's times.");
        }

        public ConferenceEvent(int eventID)
        {
            DataTable table = WebDataUtility.Instance.webAppTable("sp_admin_GetAllConferenceEvents",
                new GenericCmdParameter[] { new GenericCmdParameter("@conferenceID", Conference.Instance.ID),
                                            new GenericCmdParameter("@eventID", eventID) });

            if(table.Rows.Count == 1)
            {
                DataRow row = table.Rows[0];

                _id = DBNullable.ToInt(row["eventID"]);
                _start = DBNullable.ToDateTime(row["eventStart"]);
                _stop = DBNullable.ToDateTime(row["eventStop"]);
                _location = DBNullable.ToString(row["eventRoom"]);
                _type = Convert.ToString(row["eventType"]);
                _title = DBNullable.ToString(row["eventText"]);
                _parentID = DBNullable.ToInt(row["parentEventID"]);
                _media = DBNullable.ToString(row["eventMedia"]);
                _maxRequests = DBNullable.ToInt(row["eventMaxRequest"]);
                _isPublic = DBNullable.ToBool(row["isPublic"]);
                _lastUpdated = DBNullable.ToDateTime(row["lastUpdated"]);
                 
                object venueID = null;
                WebDataUtility.Instance.webAppScalar("sp_GetVenueIDFromRoomName",
                    new GenericCmdParameter[] { new GenericCmdParameter("@roomName", _location) }, ref venueID);
                _venueID = DBNullable.ToInt(venueID);

            }
        }

        public bool Upload()
        {
            object retval = false;
            _lastUpdated = DateTime.Now;
            WebDataUtility.Instance.webAppScalar("sp_admin_LoadEvent",
                new GenericCmdParameter[] { new GenericCmdParameter("@event", ToTable()) }, ref retval);
            
            _id = DBNullable.ToInt(retval);
            
            return DBNullable.ToInt(retval) != 0;
        }

        public bool Delete()
        {
            object retval = false;
            WebDataUtility.Instance.webAppCmd("sp_admin_RemoveEvent",
                new GenericCmdParameter[] { new GenericCmdParameter("@event", ToTable()) }, ref retval);

            return DBNullable.ToInt(retval) == 0;
        }

        public string GetParentPath()
        {
            DataTable table = WebDataUtility.Instance.webAppTable("sp_GetEventParentPath", 
                new GenericCmdParameter[] { new GenericCmdParameter("@eventID", _id) });

            return DBNullable.ToString(table.Rows[0]["Hierarchy"]);
        }

        public DataTable ToTable()
        {
            DataTable table = new DataTable();
            table.Columns.Add(new DataColumn("eventID", typeof(int)));
            table.Columns.Add(new DataColumn("conferenceID", typeof(int)));
            table.Columns.Add(new DataColumn("eventStart", typeof(DateTime)));
            table.Columns.Add(new DataColumn("eventStop", typeof(DateTime)));
            table.Columns.Add(new DataColumn("eventRoom", typeof(string)));
            table.Columns.Add(new DataColumn("eventType", typeof(string)));
            table.Columns.Add(new DataColumn("eventText", typeof(string)));
            table.Columns.Add(new DataColumn("parentEventID", typeof(int)));
            table.Columns.Add(new DataColumn("eventMedia", typeof(string)));
            table.Columns.Add(new DataColumn("eventMaxRequest", typeof(int)));
            table.Columns.Add(new DataColumn("isPublic", typeof(bool)));
            table.Columns.Add(new DataColumn("lastUpdated", typeof(DateTime)));

            DataRow row = table.NewRow();
            row["eventID"] = _id;
            row["conferenceID"] = Conference.Instance.ID;
            row["eventStart"] = _start ;
            row["eventStop"] = _stop;
            row["eventRoom"] = _location;
            row["eventType"] = _type;
            row["eventText"] = _title;
            row["parentEventID"] = _parentID;
            row["eventMedia"] = _media;
            row["eventMaxRequest"] = _maxRequests;
            row["isPublic"] = _isPublic;
            row["lastUpdated"] = _lastUpdated;

            table.Rows.Add(row);

            return table;
        }

        private bool validateDates(DateTime start, DateTime stop)
        {
            if (start > stop)
                return false;

            if (_parentID > 0)
            {
                object retval = false;

                WebDataUtility.Instance.webAppScalar("sp_ValidateNewEventDatesToParent",
                    new GenericCmdParameter[] {new GenericCmdParameter("@parentEventID", _parentID),
                                           new GenericCmdParameter("@startDate", start),
                                           new GenericCmdParameter("@stopDate", stop) }, ref retval);
                return Convert.ToBoolean(retval);
            }
            return true;
        }
    }
}