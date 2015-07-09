using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using Oracle.DataAccess.Client;
using Oracle.DataAccess.Types;

namespace DataProcess
{
    public class OracleHelper
    {
        // The Oracle Connection
        private OracleConnection conn = new OracleConnection();

        // Method ================================================================
        public OracleHelper(string connStr)
        {
            conn.ConnectionString = connStr;
        }

        public void NonQuery(string sql)
        {
            try {
                // Open connection
                conn.Open();

                OracleCommand cmd = new OracleCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }
            catch (Exception) {
                throw;
            }
            finally {
                conn.Close();
            }
        }

        public int NonQuery(string sql, string[] paramArr, object[] valArr)
        {
            try {
                // Open connection
                conn.Open();

                OracleCommand cmd = new OracleCommand(sql, conn);

                //添加参数
                if (paramArr != null && paramArr.Count() > 0) {
                    for (int i = 0; i < paramArr.Count(); i++) {
                        object val = DBNull.Value;
                        if (valArr[i] != null) {
                            val = valArr[i];
                        }
                        OracleParameter param = new OracleParameter(paramArr[i], val);
                        cmd.Parameters.Add(param);
                    }
                }

                return cmd.ExecuteNonQuery();
            }
            catch (Exception) {
                throw;
            }
            finally {
                conn.Close();
            }
        }

        public DataTable Query(string sql)
        {
            DataTable dt = new DataTable();
            OracleDataAdapter adapter = new OracleDataAdapter(sql, conn.ConnectionString);

            try {
                adapter.Fill(dt);
                return dt;
            }
            catch (Exception) {
                throw;
            }
            finally {
                adapter.Dispose();
            }
        }

        public DataTable Query(string sql, string[] paramArr, object[] valArr)
        {
            DataTable dt = new DataTable();

            OracleCommand cmd = new OracleCommand(sql, conn);
            //添加参数
            if (paramArr != null && paramArr.Count() > 0) {
                for (int i = 0; i < paramArr.Count(); i++) {
                    object val = DBNull.Value;
                    if (valArr[i] != null) {
                        val = valArr[i];
                    }
                    OracleParameter param = new OracleParameter(paramArr[i], val);
                    cmd.Parameters.Add(param);
                }
            }
            OracleDataAdapter adapter = new OracleDataAdapter(cmd);

            try {
                adapter.Fill(dt);
                return dt;
            }
            catch (Exception) {
                throw;
            }
            finally {
                adapter.Dispose();
            }
        }
    }
}
