using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Mail;
using System.Net;

namespace mnn.util {
    public class SmtpSender {
        public static bool Send(string subject, string content)
        {
            try {
                SmtpClient client = new SmtpClient("smtp.163.com");

                //构造一个发件人地址对象
                MailAddress from = new MailAddress("zjhthtalert@163.com", "zjhthtalert", Encoding.UTF8);
                //构造一个收件人地址对象
                MailAddress to = new MailAddress("598319871@qq.com", "598319871", Encoding.UTF8);

                MailMessage message = new MailMessage(from, to);
                //添加邮件主题和内容
                message.Subject = subject;
                message.SubjectEncoding = Encoding.UTF8;
                message.Body = content;
                message.BodyEncoding = Encoding.UTF8;

                //设置邮件的信息
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                message.BodyEncoding = System.Text.Encoding.UTF8;
                message.IsBodyHtml = false;
                client.EnableSsl = true;

                //设置用户名和密码。
                client.UseDefaultCredentials = false;
                string username = "zjhthtalert";
                string passwd = "htht12345678";
                //用户登陆信息
                NetworkCredential credential = new NetworkCredential(username, passwd);
                client.Credentials = credential;

                client.Send(message);
                return true;
            } catch (Exception ex) {
                Console.WriteLine(ex.ToString());
                return false;
            }
        }
    }
}
