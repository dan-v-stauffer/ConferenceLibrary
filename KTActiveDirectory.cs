using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI;
using System.Xml;
using System.DirectoryServices;
using HelperFunctions;

namespace DataUtilities
{

    namespace KTActiveDirectory
    {

        public class KTLogin
        {
            private String login;

            public KTLogin(String l)
            {
                login = l;
            }

            public String[] Split(char delimiter)
            {
                return login.Split(delimiter);
            }
        }

        public class KTActiveDirectoryUser
        {
            private string _login = string.Empty;
            private string _first = string.Empty;
            private string _last = string.Empty;
            private string _dept = string.Empty;
            private string _division = string.Empty;
            private string _country = string.Empty;
            private string _email = string.Empty;
            private string _city = string.Empty;
            private string _employeeID = string.Empty;

            public string Login
            {
                get { return _login; }
            }

            public string FirstName
            {
                get { return _first; }
            }

            public string LastName
            {
                get { return _last; }
            }

            public string EmployeeID
            {
                get { return _employeeID; }
            }

            public string Department
            {
                get { return _dept; }
            }

            public string Division
            {
                get { return _division; }
            }

            public string City
            {
                get { return _city; }
            }

            public string Country
            {
                get { return _country; }
            }

            public string Email
            {
                get { return _email; }
            }

            public bool ValidLogin
            {
                get { return validLogin(new KTLogin(_login)); }
            }



            public KTActiveDirectoryUser(string email)
            {
                if (!validEmail(email))
                    _login = string.Empty;
            }


            public KTActiveDirectoryUser(KTLogin loginName)
            {
                if(!validLogin(loginName))
                    _login = string.Empty;
            }

            private Boolean validEmail(String email)
            {
                string[] names = email.Split('\\');

                using (DirectorySearcher search = new DirectorySearcher("GC:"))
                {
                    try
                    {
                        search.SearchRoot.Path = "LDAP://kla-tencor.com/DC=adcorp,DC=kla-tencor,DC=com";
                        search.Filter = String.Format("(mail={0})", names.Length > 1 ? names[1] : names[0]);

                        search.PropertiesToLoad.Add("samaccountname");
                        search.PropertiesToLoad.Add("givenName");
                        search.PropertiesToLoad.Add("sn");
                        search.PropertiesToLoad.Add("employeeID");
                        search.PropertiesToLoad.Add("department");
                        search.PropertiesToLoad.Add("c");
                        search.PropertiesToLoad.Add("l");
                        search.PropertiesToLoad.Add("mail");

                        SearchResult result = search.FindOne();

                        if (result != null)
                        {

                            _login = result.Properties["samaccountname"][0].ToString();
                            _first = result.Properties["givenName"][0].ToString();
                            _last = result.Properties["sn"][0].ToString();
                            _dept = result.Properties["department"][0].ToString();
                            _country = result.Properties["c"][0].ToString();
                            _email = result.Properties["mail"][0].ToString();
                            _city = result.Properties["l"][0].ToString();
                            _employeeID = result.Properties["employeeID"][0].ToString();

                            getDivision();
                        }

                        return result != null;
                    }
                    finally
                    {
                        search.Dispose();
                    }
                }
            }

            public Boolean validLogin(KTLogin login)
            {
                string[] names = login.Split('\\');
                using (System.Web.Hosting.HostingEnvironment.Impersonate())
                {
                    using (DirectorySearcher search = new DirectorySearcher("GC:"))
                    {
                        try
                        {
                            search.SearchRoot.Path = "LDAP://kla-tencor.com/DC=adcorp,DC=kla-tencor,DC=com";
                            search.Filter = String.Format("(samaccountname={0})", names[names.Length - 1]);

                            search.PropertiesToLoad.Add("samaccountname");
                            search.PropertiesToLoad.Add("givenName");
                            search.PropertiesToLoad.Add("sn");
                            search.PropertiesToLoad.Add("employeeID");
                            search.PropertiesToLoad.Add("department");
                            search.PropertiesToLoad.Add("c");
                            search.PropertiesToLoad.Add("l");
                            search.PropertiesToLoad.Add("mail");

                            SearchResult result = search.FindOne();

                            if (result != null)
                            {

                                _login = result.Properties["samaccountname"][0].ToString();
                                _first = result.Properties["givenName"][0].ToString();
                                _last = result.Properties["sn"][0].ToString();
                                _dept = result.Properties["department"][0].ToString();
                                _country = result.Properties["c"][0].ToString();
                                _email = result.Properties["mail"][0].ToString();
                                _city = result.Properties["l"][0].ToString();
                                _employeeID = result.Properties["employeeID"][0].ToString();

                                getDivision();
                            }

                            return result != null;
                        }
                        finally
                        {
                            search.Dispose();
                        }
                    }
                }
            }

            private void fillKTUsers()
            {

            }

            private void getName(String email)
            {

                using (DirectorySearcher search = new DirectorySearcher("GC:"))
                {
                    search.SearchRoot.Path = "LDAP://kla-tencor.com/DC=adcorp,DC=kla-tencor,DC=com";
                    search.Filter = String.Format("(mail={0})", email);
                    search.PropertiesToLoad.Add("samaccountname");
                    search.PropertiesToLoad.Add("givenName");
                    search.PropertiesToLoad.Add("sn");
                    search.PropertiesToLoad.Add("employeeID");
                    search.PropertiesToLoad.Add("department");
                    search.PropertiesToLoad.Add("c");
                    search.PropertiesToLoad.Add("l");
                    search.PropertiesToLoad.Add("mail");

                    SearchResult result = search.FindOne();
                    if (result != null)
                    {
                        _first = result.Properties["givenName"][0].ToString();
                        _last = result.Properties["sn"][0].ToString();
                        _dept = result.Properties["department"][0].ToString();
                        _country = result.Properties["c"][0].ToString();
                        _email = result.Properties["mail"][0].ToString();
                        _city = result.Properties["l"][0].ToString();
                        _employeeID = result.Properties["employeeID"][0].ToString();
                     
                    }
                }
            }

            private void getDivision()
            {
                DataUtilities.SQLServer.WebDataUtility util = DataUtilities.SQLServer.WebDataUtility.Instance;
                object division = null;

                util.webAppScalar("sp_GetDivisionID",
                    new SQLServer.GenericCmdParameter[] { new SQLServer.GenericCmdParameter("@adDepartment", this._dept) }, ref division);
                this._division = Convert.ToString(division);
            }
        }
        //database access helper class to eliminate redundant code calling stored procedures.
    }
}

    //protected void bulkUploadEmails(object sender, EventArgs e)
    //{
    //    DirectoryEntry dirEnt = new DirectoryEntry("LDAP://kla-tencor.com/DC=adcorp,DC=kla-tencor,DC=com" );
    //    string[] loadProps = new string[] { "givenName", "sn", "telephoneNumber","ipPhone", "mail" };


    //    DataTable table = new DataTable("users");
    //    table.Columns.Add(new DataColumn("userEmail", typeof(string)));
    //    table.Columns.Add(new DataColumn("userFirstName", typeof(string)));
    //    table.Columns.Add(new DataColumn("userLastName", typeof(string)));
    //    table.Columns.Add(new DataColumn("userWorkPhone", typeof(string)));
    //    table.Columns.Add(new DataColumn("lastUpdated", typeof(DateTime)));


    //    using (DirectorySearcher search = new DirectorySearcher(dirEnt, "(&(objectClass=user)(xsupervisorid=*))", loadProps))
    //    {
    //        try
    //        {

    //            string _first = string.Empty;
    //            string _last = string.Empty;
    //            string _email = string.Empty;
    //            string _workPhone = string.Empty;

    //            search.PageSize = 100000;
    //            search.PropertiesToLoad.Add("givenName");
    //            search.PropertiesToLoad.Add("sn");
    //            search.PropertiesToLoad.Add("telephoneNumber");
    //            search.PropertiesToLoad.Add("ipPhone");
    //            search.PropertiesToLoad.Add("mail");
    //            int i = 0;
    //            SearchResultCollection results = search.FindAll();
    //                foreach(SearchResult result in results)
    //                {
    //                    if (result != null)
    //                    {
    //                        _first = string.Empty;
    //                        _last = string.Empty;
    //                        _email = string.Empty;
    //                        _workPhone = string.Empty;

    //                        try
    //                        {
    //                            _email = result.Properties["mail"][0].ToString();
    //                            _first = result.Properties["givenName"][0].ToString();
    //                            _last = result.Properties["sn"][0].ToString();
    //                            _workPhone = result.Properties["telephoneNumber"][0].ToString();

    //                            DataRow newRow = table.NewRow();
    //                            newRow["userEmail"] = _email;
    //                            newRow["userFirstName"] = _first;
    //                            newRow["userLastName"] = _last;
    //                            newRow["userWorkPhone"] = _workPhone;
    //                            newRow["lastUpdated"] = DateTime.Now;
    //                            table.Rows.Add(newRow);
    //                            System.Diagnostics.Debug.Print(i++ + "- email: " + _email);
    //                        }
    //                        catch(Exception ex)
    //                        {
    //                            if (_email != string.Empty && _first != string.Empty && _last != string.Empty)
    //                            {
    //                                DataRow newRow = table.NewRow();
    //                                newRow["userEmail"] = _email;
    //                                newRow["userFirstName"] = _first;
    //                                newRow["userLastName"] = _last;
    //                                newRow["userWorkPhone"] = _workPhone;
    //                                newRow["lastUpdated"] = DateTime.Now;
    //                                table.Rows.Add(newRow);
    //                                System.Diagnostics.Debug.Print(i++ + "- email: " + _email);
    //                            }


    //                            System.Diagnostics.Debug.Print("email: " + _email + " : " + ex.Message);
    //                        }
    //                   }
    //                }
    //                if (table.Rows.Count != 0)
    //                {
    //                    object retval = null;
    //                    dataUtil.webAppCmd("sp_BulkUserUpload", new GenericCmdParameter[] { new GenericCmdParameter("@userTable", table) }, ref retval);
    //                    //sp_BulkUploadTable
    //                }
    //        }
    //        finally
    //        {
    //            search.Dispose();
    //        }

    //    }

    //}
