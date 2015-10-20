using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EnvCenter
{
    class SvcUnit
    {
        public string TermInfo { get; set; }

        public SvcUnit(char[] info)
        {
            TermInfo = new string(info);
        }
    }
}
