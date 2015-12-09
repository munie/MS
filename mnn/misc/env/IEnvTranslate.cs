using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mnn.misc.env {
    public class SMsgTrans {
        public static readonly string TRANSLATE = "Translate";
    }

    public interface IEnvTranslate {
        string Translate(string msg);
    }
}
