using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI;
using System.Xml;
using HelperFunctions;
using ConferenceLibrary;
using System.Web.UI;

/// <summary>
/// Summary description for Common
/// </summary>
/// 
namespace HelperFunctions
{
    public static class Common
    {
        public static String[] categories = {@"System Architectures", @"Optical and Opto-Mechanical Design", @"Algorithms",
                           @"Illumination Sources", @"Stage Design", @"Sensors", @"Software Architecture", 
                           @"Software Design and Testing", @"Computational Platforms", 
                           @"Photocontamination Control", @"Contamination Control", @"Matching", 
                           @"Ease of Use", @"Reliability Testing", @"Project Management", 
                           @"Engineering Development Methods", @"Design for Manufacturability", 
                           @"Design for Serviceability", @"New Technologies or Product Ideas", @"Other"};

        public static String[] alphabet = {@"A",@"B",@"C",@"D",@"E",@"F",@"G",@"H",@"I",@"J",@"K",@"L",@"M",@"N",@"O",
                                            @"P",@"Q",@"R",@"S",@"T",@"U",@"V",@"W",@"X",@"Y",@"Z"};


        public static List<T> GetAllControlsOfType<T>(Control parent) where T : Control
        {
            List<T> result = new List<T>();

            foreach (Control control in parent.Controls)
            {
                if (control is T)
                {
                    result.Add((T)control);
                }
                if (control.HasControls())
                {
                    result.AddRange(Common.GetAllControlsOfType<T>(control));
                }
            }
            return result;
        }

        public static class HtmlRemoval
        {
            /// <summary>
            /// Remove HTML from string with Regex.
            /// </summary>
            public static string StripTagsRegex(string source)
            {
                source = RemoveLineBreaks(source);
                source = Regex.Replace(source, "<.*?>", string.Empty);
                return Trim(source);
            }


            public static string RemoveLineBreaks(string source)
            {
                return Regex.Replace(source, @"\r\n?|\n", String.Empty);
            }

            public static string Trim(string source)
            {
                //
                // Use the ^ to always match at the start of the string.
                // Then, look through all WHITESPACE characters with \s
                // Use + to look through more than 1 characters
                // Then replace with an empty string.
                //
                source = source.Replace("&#160;", " ");
                source = source.Replace("â€‹", " ");

                source = Regex.Replace(source, @"^\s+", String.Empty);

                //
                // The same as above, but with a $ on the end.
                // This requires that we match at the end.
                //
                source = Regex.Replace(source, @"\s+$", String.Empty);
                source = source.Trim();
                return source;
            }
            /// <summary>
            /// Compiled regular expression for performance.
            /// </summary>
            static Regex _htmlRegex = new Regex("<.*?>", RegexOptions.Compiled);

            /// <summary>
            /// Remove HTML from string with compiled Regex.
            /// </summary>
            public static string StripTagsRegexCompiled(string source)
            {
                source = RemoveLineBreaks(source);
                source = _htmlRegex.Replace(source, string.Empty);
                return Trim(source);
            }

            /// <summary>
            /// Remove HTML tags from string using char array.
            /// </summary>
            public static string StripTagsCharArray(string source)
            {
                char[] array = new char[source.Length];
                int arrayIndex = 0;
                bool inside = false;

                for (int i = 0; i < source.Length; i++)
                {
                    char let = source[i];
                    if (let == '<')
                    {
                        inside = true;
                        continue;
                    }
                    if (let == '>')
                    {
                        inside = false;
                        continue;
                    }
                    if (!inside)
                    {
                        array[arrayIndex] = let;
                        arrayIndex++;
                    }
                }
                string retval = new string(array, 0, arrayIndex);
                retval = RemoveLineBreaks(retval);
                return Trim(retval);
            }
        }

        public static DataTable GetDateTableFromDateRange(DateTime start, DateTime stop, Dictionary<string, Type> dataFields, string dateFormat, bool insertHTMLTags)
        {
            DataTable table = new DataTable("tbl_Dates");
            String dateValue = string.Empty;
            String dateText = string.Empty;
            foreach (KeyValuePair<string, Type> item in dataFields)
            {
                table.Columns.Add(new DataColumn(item.Key, item.Value));
                if (item.Value == typeof(string))
                    dateText = item.Key;
                if (item.Value == typeof(DateTime))
                    dateValue = item.Key;
            }
            int noDays = (stop.Date - start.Date).Days;
            DateTime temp = start;
            for (int i = 1; i <= noDays; i++)
            {
                DataRow dr = table.NewRow();
                dr[dateValue] = temp.Date.ToString("MM/dd/yy hh:mm tt");

                string modifier = "th";
                if (Convert.ToString(temp.Day).EndsWith("1"))
                    modifier = "st";
                else if (Convert.ToString(temp.Day).EndsWith("2"))
                    modifier = "nd";

                dr[dateText] = temp.ToString(dateFormat);
                temp = temp.AddDays(1);
                table.Rows.Add(dr);
            }
            return table;
        }

        public static string ToRoman(int number)
        {
            if ((number < 0) || (number > 3999)) throw new ArgumentOutOfRangeException("insert value betwheen 1 and 3999");
            if (number < 1) return string.Empty;
            if (number >= 1000) return "M" + ToRoman(number - 1000);
            if (number >= 900) return "CM" + ToRoman(number - 900); //EDIT: i've typed 400 instead 900
            if (number >= 500) return "D" + ToRoman(number - 500);
            if (number >= 400) return "CD" + ToRoman(number - 400);
            if (number >= 100) return "C" + ToRoman(number - 100);
            if (number >= 90) return "XC" + ToRoman(number - 90);
            if (number >= 50) return "L" + ToRoman(number - 50);
            if (number >= 40) return "XL" + ToRoman(number - 40);
            if (number >= 10) return "X" + ToRoman(number - 10);
            if (number >= 9) return "IX" + ToRoman(number - 9);
            if (number >= 5) return "V" + ToRoman(number - 5);
            if (number >= 4) return "IV" + ToRoman(number - 4);
            if (number >= 1) return "I" + ToRoman(number - 1);
            throw new ArgumentOutOfRangeException("something bad happened");
        }

        public static string CreateConfirmationNumber(int id)
        {
            string charPool = "A9B8C7D6E5FG4O3P2Q1R0ZSYT0X9U8N7V6J5W4K3H2L1I";
            Random rndm = new Random(id);
            string str = "";
            int rNo;
            for (int i = 0; i < 8; i++)
            {
                if (i % 2 == 0)
                {
                    str += rndm.Next(10).ToString();
                }
                else
                {
                    rNo = rndm.Next(52);
                    if (rNo < charPool.Length)
                        str = String.Concat(str, charPool[rNo]);
                }
            }
            return str;
        }

        public static List<string> initTitleCaseExemptions()
        {
            List<string> exempt = new List<string>();

            exempt.Add("And ");
            exempt.Add(" A ");
            exempt.Add("The ");
            exempt.Add("Of ");
            exempt.Add("An ");
            exempt.Add("But ");
            exempt.Add("Or ");
            exempt.Add("Nor ");
            exempt.Add("But ");
            exempt.Add("For ");
            exempt.Add("As ");
            exempt.Add("So ");
            exempt.Add("On ");
            exempt.Add("Yet ");
            exempt.Add("In ");
            exempt.Add("So ");
            exempt.Add("To ");
            exempt.Add("Vs ");
            List<string> hyphenList = new List<string>();

            //handle hyphenated phrases
            foreach (string exemptWord in exempt)
            {
                string word = string.Empty;

                word = exemptWord.Replace(" ", "-");
                if(!word.Equals("-A-"))
                    word = "-" + word;
                hyphenList.Add(word);
            }

            exempt.InsertRange(exempt.Count - 1, hyphenList);
            return exempt;
        }

        private static string UppercaseFirst(string s)
        {
            StringBuilder newString = new StringBuilder();
            StringBuilder nextString = new StringBuilder();
            string[] phraseArray;
            string theWord;
            string returnValue;
            phraseArray = s.Split(null);
            for (int i = 0; i < phraseArray.Length; i++)
            {
                theWord = phraseArray[i];
                if (theWord.Length > 1)
                {
                    if (theWord.Substring(1, 1) == "'")
                    {
                        //Process word with apostrophe at position 1 in 0 based string.
                        if (nextString.Length > 0)
                            nextString.Replace(nextString.ToString(), null);
                        nextString.Append(theWord.Substring(0, 1).ToUpper());
                        nextString.Append("'");
                        nextString.Append(theWord.Substring(2, 1).ToUpper());
                        nextString.Append(theWord.Substring(3).ToLower());
                        nextString.Append(" ");
                    }
                    else
                    {
                        if (theWord.Length > 1 && theWord.Substring(0, 2) == "mc")
                        {
                            //Process McName.
                            if (nextString.Length > 0)
                                nextString.Replace(nextString.ToString(), null);
                            nextString.Append("Mc");
                            nextString.Append(theWord.Substring(2, 1).ToUpper());
                            nextString.Append(theWord.Substring(3).ToLower());
                            nextString.Append(" ");
                        }
                        else
                        {
                            if (theWord.Length > 2 && theWord.Substring(0, 3) == "mac")
                            {
                                //Process MacName.
                                if (nextString.Length > 0)
                                    nextString.Replace(nextString.ToString(), null);
                                nextString.Append("Mac");
                                nextString.Append(theWord.Substring(3, 1).ToUpper());
                                nextString.Append(theWord.Substring(4).ToLower());
                                nextString.Append(" ");
                            }
                            else
                            {
                                //Process normal word (possible apostrophe near end of word.
                                if (nextString.Length > 0)
                                    nextString.Replace(nextString.ToString(), null);
                                nextString.Append(theWord.Substring(0, 1).ToUpper());
                                nextString.Append(theWord.Substring(1));
                                nextString.Append(" ");
                            }
                        }
                    }

                    string[] hypenArray = theWord.Split('-');
                    if (hypenArray.Length > 1)
                    {
                        nextString.Clear();

                        for (int j = 0; j < hypenArray.Length; j++)
                        {
                            string hypenWord = hypenArray[j];
                            if (hypenWord.Length > 0)
                            {
                                if (hypenWord.Length > 1)
                                {
                                    if (theWord.Substring(1, 1) == "'")
                                    {
                                        //Process word with apostrophe at position 1 in 0 based string.
                                        nextString.Append(hypenWord.Substring(0, 1).ToUpper());
                                        nextString.Append("'");
                                        nextString.Append(hypenWord.Substring(2, 1).ToUpper());
                                        nextString.Append(hypenWord.Substring(3).ToLower());
                                        nextString.Append("-");
                                    }
                                    else
                                    {
                                        if (theWord.Length > 1 && theWord.Substring(0, 2) == "mc")
                                        {
                                            //Process McName.
                                            nextString.Append("Mc");
                                            nextString.Append(hypenWord.Substring(2, 1).ToUpper());
                                            nextString.Append(hypenWord.Substring(3).ToLower());
                                            nextString.Append("-");
                                        }
                                        else
                                        {
                                            if (theWord.Length > 2 && theWord.Substring(0, 3) == "mac")
                                            {
                                                //Process MacName.
                                                nextString.Append("Mac");
                                                nextString.Append(hypenWord.Substring(3, 1).ToUpper());
                                                nextString.Append(hypenWord.Substring(4).ToLower());
                                                nextString.Append("-");
                                            }
                                            else
                                            {
                                                //Process normal word (possible apostrophe near end of word.

                                                nextString.Append(hypenWord.Substring(0, 1).ToUpper());
                                                nextString.Append(hypenWord.Substring(1));
                                                nextString.Append("-");
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    nextString.Append(hypenWord.Substring(0, 1).ToUpper());
                                    nextString.Append(hypenWord.Substring(1));
                                    nextString.Append("-");
                                }
                            }
                        }
                        nextString.Replace("-"," ", nextString.ToString().Length-1,1);
                    }
                }
                else
                {
                    //Process normal single character length word.
                    if (nextString.Length > 0)
                        nextString.Replace(nextString.ToString(), null);
                    nextString.Append(theWord.ToUpper());
                    nextString.Append(" ");
                }
                newString.Append(nextString);
            }
            returnValue = newString.ToString();
            return returnValue.Trim();
        }

        public static string ProperCase(string Input)
        {
            if (Input.Length == 0)
                return string.Empty;

            string output;
            output = UppercaseFirst(Input);
            string substring = output.Substring(1, output.Length - 1);

            List<string> exempt = initTitleCaseExemptions();
            foreach (string exemption in exempt)
            {
                substring = substring.Replace(exemption, exemption.ToLower());
            }

            //catch special circumstances - exemption words that start off a new subphrase.

            List<string> subphraseStarts = new List<string>();
            subphraseStarts.Add(". ");
            subphraseStarts.Add(": ");
            subphraseStarts.Add("; ");
            subphraseStarts.Add("(");
            subphraseStarts.Add("- ");
            subphraseStarts.Add("[");
            subphraseStarts.Add("'");
            subphraseStarts.Add("\"");
            subphraseStarts.Add("{");

            foreach (string sub in subphraseStarts)
            {
                foreach (string exemption in exempt)
                {
                    if (exemption.Substring(0, 1) == "-")
                        continue;
                    else
                    {
                        string temp = exemption;
                        temp = sub + temp.ToLower();
                        substring = substring.Replace(temp, UppercaseFirst(temp) + " ");
                    }
                }
            }


            return output.Substring(0, 1) + substring;
        }


        public static string GetDaySuffix(DateTime date)
        {
            int day = date.Day;

            switch (day)
            {
                case 1:
                case 21:
                case 31:
                    return "st";
                case 2:
                case 22:
                    return "nd";
                case 3:
                case 23:
                    return "rd";
                default:
                    return "th";
            }
        }

        public static int Convert24To12Hours(int hour)
        {
            if (hour == 0)
                return 12;
            if (hour <= 12)
                return hour;
            else
                return hour - 12;
        }

        public static int Convert12To24Hours(int hour, string meridian)
        {
            if (hour == 12 && meridian.ToUpper().Equals("AM"))
                return 0;
            if (hour <= 11 && meridian.ToUpper().Equals("PM"))
                return hour + 12;
            else
                return hour;
        }

    }

}
