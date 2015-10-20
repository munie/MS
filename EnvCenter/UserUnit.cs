using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EnvCenter
{
    class UserUnit
    {
        public string Userid { get; set; }
        public string Passwd { get; set; }
        public DateTime LoginTime { get; set; }

        public UserUnit(char[] userid, char[] passwd)
        {
            Userid = new string(userid);
            Passwd = new string(passwd);
            LoginTime = DateTime.Now;
        }
    }
}
