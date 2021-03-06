﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using Newtonsoft.Json;
using UDS.Entity;
using System.Data.Common;
using System.Data;

namespace UDS.SubModule.bulletin
{
    public partial class bulletinAction : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string UserID = null == Request.Cookies["UserID"] ? "" : Request.Cookies["UserID"].Value;

            switch (Request.HttpMethod)
            {
                case "GET":
                    string method = Request.Params["m"];

                    if (!string.IsNullOrWhiteSpace(method))
                    {
                        try
                        {
                            switch (method)
                            {
                                case "uuid":
                                    string newuuid = Guid.NewGuid().ToString();
                                    string dir = Path.Combine(Server.MapPath("~/App_Browsers"), newuuid);
                                    if (!Directory.Exists(dir))
                                        Directory.CreateDirectory(dir);

                                    Response.Write(newuuid);
                                    break;
                                default:
                                    Response.StatusCode = 400;
                                    Response.Write("错误的请求");
                                    break;
                            }
                        }
                        catch (Exception eX)
                        {
                            Response.StatusCode = 400;
                            Response.Write(eX.Message);
                        }
                        finally
                        {
                            Response.End();
                        }
                    }
                    else
                    {
                        string startIndex = Request.Params["startIndex"];
                        string orderby = Request.Params["orderby"];
                        string order = Request.Params["order"];
                        string rows = Request.Params["rows"];
                        string type = Request.Params["type"];

                        if (string.IsNullOrEmpty(startIndex))
                            startIndex = "1";
                        if (string.IsNullOrEmpty(rows))
                            rows = "10";

                        int rowstart = int.Parse(startIndex);
                        int rowend = int.Parse(rows) + rowstart - 1;

                        string sqlTemplate = "select * from " +
                            "(select bulletinid, subject, contents, createtime, sendtime, " +
                                "(select count(*) from uds_bulletinreadlist t where t.bulletinid = b.bulletinid {2}) as readcount," +
                                "ROW_NUMBER() over (order by sendtime desc) as rowno " +
                                "from uds_bulletin b) as A where rowno >= {0} and rowno <= {1} {3} order by sendtime desc";

                        string countTemplate = "select count(*) from uds_bulletin {0}";

                        string sql = "";
                        string countsql = "";

                        switch (type)
                        {
                            case "1":
                                //获取全部公告
                                sql = string.Format(sqlTemplate, rowstart, rowend, "", "");
                                countsql = string.Format(countTemplate, "");
                                break;
                            case "2":
                                //获取当前用户的全部未读公告
                                sql = string.Format(sqlTemplate, rowstart, rowend,
                                    "and t.staffid = '" + UserID + "'", " and readcount = 0");
                                countsql = string.Format(countTemplate,
                                    "where (bulletinid not in (select bulletinid from uds_bulletinreadlist where staffid = '" + UserID + "'))");
                                break;
                            case "3":
                                sql = string.Format(sqlTemplate, rowstart, rowend,
                                   "and t.staffid = '" + UserID + "'", "");
                                //获取当前用户的全部公告
                                countsql = string.Format(countTemplate, "");
                                break;
                            case "5":
                                sql = "select bulletinid, subject, contents, createtime, sendtime, " +
                                "(select count(*) from uds_bulletinreadlist t where t.bulletinid = b.bulletinid) as readcount " +
                                "from uds_bulletin b where (bulletinid not in (select bulletinid from uds_bulletinreadlist where staffid = '" + UserID + "')) " +
                                "order by sendtime desc";
                                break;
                        }

                        switch (type)
                        {
                            case "1":
                            case "2":
                            case "3":
                                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["default"].ConnectionString))
                                {
                                    SqlCommand comm = new SqlCommand(string.Format(sql, rowstart, rowend), con);
                                    SqlDataReader reader = null;
                                    try
                                    {
                                        con.Open();
                                        reader = comm.ExecuteReader();
                                        IList<UDSBulletin> bulletins = new List<UDSBulletin>();
                                        while (reader.Read())
                                        {
                                            UDSBulletin buletting = new UDSBulletin()
                                            {
                                                Bulletinid = reader.GetInt32(0),
                                                Subject = reader.GetString(1),
                                                Contents = reader.GetString(2),
                                                Createtime = reader.GetDateTime(3).ToString(),
                                                Sendtime = reader.GetDateTime(4).ToString(),
                                                Readcount = reader.GetInt32(5)
                                            };

                                            bulletins.Add(buletting);
                                        }

                                        reader.Close();
                                        comm.CommandText = countsql;

                                        int totalCount = int.Parse(comm.ExecuteScalar().ToString());

                                        con.Close();

                                        PageRecords r = new PageRecords();
                                        r.Order = order;
                                        r.Orderby = orderby;
                                        r.Rows = int.Parse(rows);
                                        r.TotalRows = totalCount;
                                        r.Records = bulletins;

                                        var jsonSer = new Newtonsoft.Json.JsonSerializer();
                                        StringWriter sw = new StringWriter();
                                        using (JsonWriter jw = new JsonTextWriter(sw))
                                        {
                                            jw.Formatting = Formatting.Indented;

                                            jsonSer.Serialize(jw, r);
                                        }

                                        Response.ContentType = "application/json";

                                        Response.Write(sw.ToString());
                                        sw.Close();
                                    }
                                    catch (Exception eX)
                                    {
                                        if (null != reader && !reader.IsClosed)
                                            reader.Close();

                                        if (System.Data.ConnectionState.Open == con.State)
                                            con.Close();

                                        Response.StatusCode = 400;
                                        Response.ContentType = "text/html";
                                        Response.Write(eX.Message);
                                    }
                                    finally
                                    {
                                        Response.End();
                                    }
                                }
                                break;
                            case "4":
                                //获取指定ID的记录
                                string id = Request.Params["id"];
                                sql = string.Format("select bulletinid, subject, contents, createtime, sendtime from uds_bulletin where bulletinid = {0}", id);
                                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["default"].ConnectionString))
                                {
                                    SqlCommand comm = new SqlCommand(sql, con);
                                    SqlDataReader reader = null;
                                    SqlDataReader attachReader = null;

                                    try
                                    {
                                        con.Open();

                                        reader = comm.ExecuteReader();

                                        UDSBulletin buletting = new UDSBulletin();

                                        if (reader.Read())
                                        {
                                            buletting.Bulletinid = reader.GetInt32(0);
                                            buletting.Subject = reader.GetString(1);
                                            buletting.Contents = reader.GetString(2);
                                            buletting.Createtime = reader.GetDateTime(3).ToString();
                                            buletting.Sendtime = reader.GetDateTime(4).ToString();

                                            reader.Close();

                                            sql = string.Format(
                                                "select bulletinid, attachmentid, attachmentname, attachmentpath from UDS_BulletinAttachment where bulletinid = {0}",
                                                id);

                                            comm.CommandText = sql;
                                            attachReader = comm.ExecuteReader();

                                            buletting.Attaches = new List<UDSBulletinAttaches>();

                                            while (attachReader.Read())
                                            {
                                                UDSBulletinAttaches a = new UDSBulletinAttaches()
                                                {
                                                    Bulletinid = attachReader.GetInt32(0),
                                                    Attachmentid = attachReader.GetInt32(1),
                                                    Attachmentname = attachReader.GetString(2),
                                                    Attachmentpath = attachReader.GetString(3)
                                                };

                                                buletting.Attaches.Add(a);
                                            }

                                            attachReader.Close();
                                        }
                                        else
                                        {
                                            reader.Close();
                                            con.Close();

                                            buletting = new UDSBulletin();
                                            buletting.Attaches = new List<UDSBulletinAttaches>();
                                        }

                                        var jsonSer = new Newtonsoft.Json.JsonSerializer();
                                        StringWriter sw = new StringWriter();
                                        using (JsonWriter jw = new JsonTextWriter(sw))
                                        {
                                            jw.Formatting = Formatting.Indented;

                                            jsonSer.Serialize(jw, buletting);
                                        }

                                        Response.ContentType = "application/json";

                                        Response.Write(sw.ToString());
                                        sw.Close();
                                    }
                                    catch (Exception eX)
                                    {
                                        if (null != reader)
                                            reader.Close();

                                        if (null != attachReader)
                                            attachReader.Close();

                                        Response.StatusCode = 400;
                                        Response.ContentType = "text/html";
                                        Response.Write(eX.Message);
                                    }
                                    finally
                                    {
                                        if (ConnectionState.Open == con.State)
                                            con.Close();

                                        Response.End();
                                    }
                                }
                                break;
                            case "5":
                                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["default"].ConnectionString))
                                {
                                    SqlCommand comm = new SqlCommand(sql, con);
                                    SqlDataReader reader = null;

                                    try
                                    {
                                        con.Open();
                                        reader = comm.ExecuteReader();
                                        IList<UDSBulletin> bulletins = new List<UDSBulletin>();
                                        while (reader.Read())
                                        {
                                            UDSBulletin buletting = new UDSBulletin()
                                            {
                                                Bulletinid = reader.GetInt32(0),
                                                Subject = reader.GetString(1),
                                                Contents = reader.GetString(2),
                                                Createtime = reader.GetDateTime(3).ToString(),
                                                Sendtime = reader.GetDateTime(4).ToString(),
                                                Readcount = reader.GetInt32(5)
                                            };

                                            bulletins.Add(buletting);
                                        }

                                        reader.Close();
                                        con.Close();
                                        var jsonSer = new Newtonsoft.Json.JsonSerializer();
                                        StringWriter sw = new StringWriter();
                                        using (JsonWriter jw = new JsonTextWriter(sw))
                                        {
                                            jw.Formatting = Formatting.Indented;

                                            jsonSer.Serialize(jw, bulletins);
                                        }

                                        Response.ContentType = "application/json";

                                        Response.Write(sw.ToString());
                                        sw.Close();
                                    }
                                    catch (Exception eX)
                                    {
                                        if (null != reader)
                                            reader.Close();

                                        Response.StatusCode = 400;
                                        Response.Write(eX.Message);
                                    }
                                    finally
                                    {
                                        if (ConnectionState.Open == con.State)
                                            con.Close();

                                        Response.End();
                                    }
                                }
                                break;
                        }
                    }

                    break;
                case "POST":
                    string insertBulleting = "insert into UDS_Bulletin(subject, contents, createtime, sendtime) values('{0}', '', getDate(), getDate());SELECT SCOPE_IDENTITY();";
                    string insertAttache = "insert into UDS_BulletinAttachment(bulletinid, attachmentname, attachmentpath) values('{0}', '{1}', '{2}')";

                    string title = Request.Params["t"];
                    string uuid = Request.Params["uuid"];

                    string fileDir = Path.Combine(Server.MapPath("~/App_Browsers"), uuid);

                    string[] files = Directory.GetFiles(fileDir);

                    if (string.IsNullOrWhiteSpace(uuid))
                    {
                        Response.StatusCode = 400;
                        Response.Write("错误的公告发布，请刷新页面后重新尝试");
                        Response.End();
                    }
                    else
                    {
                        string exeSql = string.Format(insertBulleting, title);

                        SqlTransaction trans = null;

                        using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["default"].ConnectionString))
                        {
                            try
                            {
                                con.Open();
                                trans = con.BeginTransaction();
                                SqlCommand comm = con.CreateCommand();
                                comm.CommandText = exeSql;
                                comm.Transaction = trans;

                                int id = int.Parse(comm.ExecuteScalar().ToString());

                                foreach (string s in files)
                                {
                                    exeSql = string.Format(insertAttache, id, Path.GetFileName(s), s);
                                    comm.CommandText = exeSql;
                                    comm.ExecuteNonQuery();
                                }

                                trans.Commit();
                                con.Close();

                                Response.Write("公告发布成功");
                                Response.End();
                            }
                            catch (Exception eX)
                            {
                                if (null != trans)
                                    trans.Rollback();

                                con.Close();

                                Response.StatusCode = 400;
                                Response.Write(eX.Message);
                                Response.End();
                            }
                        }

                    }
                    break;
                case "PUT":
                    
                    break;
                case "HEAD":
                    break;
                case "DELETE":
                    break;
            }
        }
    }
}