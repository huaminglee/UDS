using System;
using System.Diagnostics;
using System.IO;


namespace UDS.Components 
{
	/// <summary>
	/// 错误处理函数，用于记录错误日志
	/// </summary>
	public class Error {
		//记录错误日志位置
		private const string FILE_NAME = "udslog.txt";

		/// <summary>
		/// 记录日志至文本文件
		/// </summary>
		/// <param name="message">记录的内容</param>
        public static void Log(string message)
        {
            //string filefullname = System.Web.HttpContext.Current.Server.MapPath(FILE_NAME);

            //if (File.Exists(filefullname))
            //{
            //    StreamWriter sr = File.AppendText(FILE_NAME);
            //    sr.WriteLine("\n");
            //    sr.WriteLine(DateTime.Now.ToString() + message);
            //    sr.Close();
            //}
            //else
            //{
            //    StreamWriter sr = File.CreateText(FILE_NAME);
            //    sr.Close();
            //}
            throw new Exception(message);
        }

    }
}
