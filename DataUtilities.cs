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
using System.Net.Mail;

/// <summary>
/// Summary description for DataUtilities
/// </summary>
namespace DataUtilities
{

    namespace SQLServer
    {
        public class WebDataUtility
        {
            private static WebDataUtility _instance;
            String connStr = ConfigurationManager.ConnectionStrings["EngConferenceDB"].ConnectionString;
            SqlConnection cxn;

            public static WebDataUtility Instance
            {
                get
                {
                        string name = "WebDataUtility";
                        if (HttpContext.Current.Session[name] == null) 
                        HttpContext.Current.Session[name] = new WebDataUtility(); 
                        return (WebDataUtility)HttpContext.Current.Session[name]; 
                }
            }

            private WebDataUtility()
            {
                
            }

            ~WebDataUtility()
            {
                if (cxn != null)
                {
                    cxn.Dispose();
                }
            }

            public void Dispose()
            {
                if (!cxn.Equals(null))
                    cxn.Dispose();
            }

            public DataTable webAppTable(string cmdName, GenericCmdParameter[] parameters)
            {
                using(cxn = new SqlConnection(connStr))
                {
                    cxn.Open();
                    SqlCommand cmd = createSQLCommand(cmdName);
                    assignParameters(parameters, cmd);
                    return getValues(cmd);
                }
               
            }

            public DataTable webAppTable(string tblName)
            {                
                using(cxn = new SqlConnection(connStr))
                {
                    cxn.Open();

                    return getValues(createSQLTable(tblName));
                }
            }

            public void webAppScalar(string cmdName, GenericCmdParameter[] parameters, ref object returnValue)
            {
                using(cxn = new SqlConnection(connStr))
                {
                    cxn.Open();

                SqlCommand cmd = createSQLCommand(cmdName);
                assignParameters(parameters, cmd);
                returnValue = cmd.ExecuteScalar();
                }
            }

            public void webAppCmd(string cmdName, GenericCmdParameter[] parameters, ref object returnValue)
            {
                using(cxn = new SqlConnection(connStr))
                {
                    cxn.Open();
                    SqlCommand cmd = createSQLCommand(cmdName);
                    assignParameters(parameters, cmd);
                    cmd.Parameters.Add("@retval", SqlDbType.Int).Direction = ParameterDirection.ReturnValue;
                    cmd.ExecuteNonQuery();
                    returnValue = cmd.Parameters["@retval"].Value;
                }
            }


            public DataTable webAppTable(SQLString sql)
            {
                using (cxn = new SqlConnection(connStr))
                {
                    return getValues(createSQLCommand(sql));
                }
            }

            public void webAppScalar(SQLString sql, GenericCmdParameter[] parameters, ref object returnValue)
            {
                using (cxn = new SqlConnection(connStr))
                {
                    SqlCommand cmd = createSQLCommand(sql);
                    assignParameters(parameters, cmd);
                    returnValue = cmd.ExecuteScalar();
                }
            }

            public void webAppCmd(SQLString sql, GenericCmdParameter[] parameters, ref object returnValue)
            {
                using (cxn = new SqlConnection(connStr))
                {
                    SqlCommand cmd = createSQLCommand(sql);
                    assignParameters(parameters, cmd);
                    cmd.ExecuteNonQuery();
                }
            }
            //private functions
            private SqlCommand createSQLCommand(string cmdName)
            {
                SqlCommand cmd = new SqlCommand(cmdName, cxn);
                cmd.CommandType = CommandType.StoredProcedure;
                return cmd;
            }

            private SqlCommand createSQLCommand(SQLString sql)
            {
                SqlCommand cmd = new SqlCommand(sql.SQL, cxn);
                cmd.CommandType = CommandType.Text;
                return cmd;
            }

            private SqlCommand createSQLTable(string tblName)
            {
                String sql = "SELECT * FROM " + tblName;
                SqlCommand sqlTbl = new SqlCommand(sql, cxn);
                sqlTbl.CommandType = CommandType.Text;
                return sqlTbl;

            }
            private DbType getDbType(Type objType)
            {
                switch (objType.ToString())
                {
                    case "System.String":
                        return DbType.String;
                    case "System.Boolean":
                        return DbType.Binary;
                    case "System.Int32":
                        return DbType.Int32;
                    case "System.DateTime":
                        return DbType.DateTime;
                    case "System.Double":
                        return DbType.Double;
                    default:
                        return DbType.String;
                }
            }

            private DataTable getValues(SqlCommand cmd)
            {
                if (cxn.State != ConnectionState.Open)
                    throw new Exception("Data connection is closed.");

                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        var tb = new DataTable();
                        tb.Load(dr);
                        return tb;
                    }
            }

            private void assignParameters(GenericCmdParameter[] parameters, SqlCommand cmd)
            {
                if (parameters == null)
                    return;
                foreach (GenericCmdParameter parameter in parameters)
                {
                        
                    cmd.Parameters.AddWithValue(parameter.ParamName, parameter.ParamValue);
                    if (parameter.ParamValue is DataTable)
                        cmd.Parameters[parameter.ParamName].SqlDbType = SqlDbType.Structured;
                }
            }

        }

        public struct GenericCmdParameter
        {
            public string ParamName;
            public object ParamValue;

            public GenericCmdParameter(string name, object value)
            {
                ParamName = name;
                ParamValue = value;
            }
        }

        public struct SQLString
        {
            public string SQL;
            public SQLString(string sql)
            {
                SQL = sql;
            }
        }

        public class MemberAttribute
        {
            private String _name;
            private Object _value;

            public String Name
            {
                get {return _name;}
            }
            public Object Value
            {
                get { return _value;}
            }
            
            public MemberAttribute(String name, Object value)
            {
                _name = name;
                _value = value;
            }
            public Type GetType()
            {
                return _value.GetType();
            }
        }

        public class MemberAttributesCollection : System.Collections.CollectionBase
        {
            public void Add(MemberAttribute attribute)
            {
                List.Add(attribute);
            }
       
            // C#
            public void Remove(int index)
            {
               // Check to see if there is a widget at the supplied index.
               if (index < Count - 1 || index > 0)
                     List.RemoveAt(index); 
            }

            public void Clear()
            {
                List.Clear();
            }

            public void Remove(string name)
            {
                foreach(MemberAttribute attribute in List)
                {
                    if(attribute.Name == name)
                    {
                        List.Remove(attribute);
                        return;
                    }
                }
            }

            public MemberAttribute Item(int Index)
            {
                // The appropriate item is retrieved from the List object and
                // explicitly cast to the Widget type, then returned to the 
                // caller.
                return (MemberAttribute) List[Index];
            }

            public MemberAttribute Item(string name)
            {
                foreach(MemberAttribute attribute in List)
                {
                    if(attribute.Name == name)
                        return attribute;
                }
                return null;
            }
        }

        public static class DBNullable
        {

            public static int ToInt(object value)
            {
                if (value == null)
                    return 0;
                if (DBNull.Value.Equals(value))
                    return 0;
                else
                {
                    int retval = 0;
                    if (int.TryParse(Convert.ToString(value), out retval))
                        return retval;
                    else
                        return 0;
                }
            }

            public static DateTime ToDateTime(object value)
            {
                if (value == null)
                    return new DateTime(1900, 1, 1);
                if (DBNull.Value.Equals(value))
                    return new DateTime(1900, 1, 1);
                else
                {
                    DateTime retval = new DateTime(1900, 1, 1);
                    if (DateTime.TryParse(Convert.ToString(value), out retval))
                        return retval;
                    else
                        return new DateTime(1900, 1, 1);
                }
            }

            public static bool ToBool(object value)
            {
                if (value == null)
                    return false;
                if (DBNull.Value.Equals(value))
                    return false;
                else
                {
                    bool retval = false;

                    string strBool = Convert.ToString(value);
                    strBool = strBool == "1" ? "true" : strBool;
                    strBool = strBool == "0" ? "false" : strBool;
                    if (bool.TryParse(strBool, out retval))
                        return retval;
                    else
                        return false;
                }
            }

            public static double ToDouble(object value)
            {
                if (value == null)
                    return 0;
                if (DBNull.Value.Equals(value))
                    return 0;
                else
                {
                    double retval = 0;
                    if (double.TryParse(Convert.ToString(value), out retval))
                        return retval;
                    else
                        return 0;
                }
            }

            public static string ToString(object value)
            {
                return Convert.ToString(value);
            }
        }

    }

    
}