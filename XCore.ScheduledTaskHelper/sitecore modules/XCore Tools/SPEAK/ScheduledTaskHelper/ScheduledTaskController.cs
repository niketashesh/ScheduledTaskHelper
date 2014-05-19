using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Mvc;
using System.Xml;
using Sitecore;
using Sitecore.Configuration;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Jobs;
using Sitecore.Reflection;
using Sitecore.Tasks;
using Sitecore.Xml;

namespace XCore.Speak.sitecore_modules.XCore_Tools.SPEAK.ScheduledTaskHelper
{
    public class ScheduledTaskController : Controller
    {
        public ActionResult GetScheduledAgents(bool includeSystemNodes = false)
        {
            var agents = new List<XmlNode>();
            try
            {
                XmlNodeList configNodes = Factory.GetConfigNodes("scheduling/agent");
                foreach (XmlNode node in configNodes)
                {
                    if (node != null && node.Attributes["type"] != null && node.Attributes["method"] != null)
                    {
                        if (!agents.Contains(node) && ShowNode(node, includeSystemNodes))
                            agents.Add(node);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("could not locate agents", ex, this);
            }
            return base.Json(new
            {
                totalRecordCount = agents.Count,
                data = agents
                    .Select(agent => new
                    {
                        ClassName = GetFriendlyClassName(agent),
                        TaskName = XmlUtil.GetAttribute("type", agent, true),
                        MethodName = XmlUtil.GetAttribute("method", agent, true),
                        itemId = agents.IndexOf(agent) + 1
                    }
                    )
            }
                , JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetScheduledTasks()
        {
            var scheduledTasks = new List<ScheduleItem>();
            try
            {
                string query = string.Format("/sitecore/*[@@id='{0}']//*[@@templateid='{1}']", ItemIDs.SystemRoot,
                    TemplateIDs.Schedule);
                scheduledTasks =
                    Factory.GetDatabase("master").SelectItems(query).Select(item => new ScheduleItem(item)).ToList();
            }
            catch (Exception ex)
            {
                Log.Error("could not locate scheduled tasks", ex, this);
            }
            return Json(new
            {
                totalRecordCount = scheduledTasks.Count,
                data = scheduledTasks.Select(task => new
                {
                    Name = task.DisplayName,
                    CommandName =
                        task.CommandItem != null
                            ? task.CommandItem.DisplayName
                            : Translate.Text("No Command Item Defined"),
                    LastRun = task.LastRun.ToString("MM/dd/yyyy HH:mm:ss"),
                    itemId = task.ID.ToString()
                })
            }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult RunAgent(string type)
        {
            try
            {
                Job.Total = 1;

                if (string.IsNullOrEmpty(type))
                    return
                        base.Json(
                            new
                            {
                                Status = "Error",
                                Success = false,
                                Message = Translate.Text("Oops! System error, please try again")
                            },
                            JsonRequestBehavior.AllowGet);
                XmlNode agentNode = GetAgentNode(type);
                if (agentNode == null)
                    return
                        base.Json(
                            new
                            {
                                Status = "Error",
                                Success = false,
                                Message = Translate.Text("Oops! System error, please try again")
                            },
                            JsonRequestBehavior.AllowGet);
                Job.AddMessage("Running {0}", new object[] { agentNode.Attributes["type"] });
                object obj2 = ReflectionUtil.CreateObject(agentNode);
                string methodName = agentNode.Attributes["method"].Value;
                if (obj2 != null)
                {
                    MethodInfo method = ReflectionUtil.GetMethod(obj2.GetType(), methodName, true, true, true,
                        new object[0]);
                    if (method != null)
                    {
                        ReflectionUtil.InvokeMethod(method, new object[0], obj2);
                    }
                }
                Job.Processed += 1;
                return Json(
                    new { Status = "Success", Success = true, Message = Translate.Text("Sucessfully executed!") },
                    JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                Log.Error("could not run agent", ex, this);
                return
                    base.Json(
                        new
                        {
                            Status = "Error",
                            Success = false,
                            Message = Translate.Text("Oops! System error, please try again")
                        },
                        JsonRequestBehavior.AllowGet);
            }
        }

        public ActionResult RunTask(string itemid)
        {
            try
            {
                Job.Total = 1;

                Item itm = Factory.GetDatabase("master").GetItem(itemid);
                var scheduledItem = new ScheduleItem(itm);
                Job.AddMessage("Running {0}", new object[] { scheduledItem.DisplayName });
                scheduledItem.Execute();
                Job.Processed += 1;
            }
            catch (Exception ex)
            {
                Log.Error("could not execute", ex, this);
                return
                    Json(
                        new
                        {
                            Status = "Error",
                            Success = false,
                            Message = Translate.Text("Oops! System error, please try again")
                        },
                        JsonRequestBehavior.AllowGet);
            }
            return Json(new { Status = "Success", Success = true, Message = Translate.Text("Sucessfully executed!") },
                JsonRequestBehavior.AllowGet);
        }

        #region helper

        private string GetFriendlyClassName(XmlNode node)
        {
            string className = XmlUtil.GetAttribute("class", node);
            if (string.IsNullOrEmpty(className))
            {
                className = XmlUtil.GetAttribute("type", node);
                int index = className.IndexOf(',');
                if (index > 0)
                    className = className.Substring(0, index).Trim();
                int lastIndex = className.LastIndexOf('.');
                if (lastIndex > 0)
                    className = className.Substring(lastIndex + 1).Trim();
            }

            return GetUpperCaseString(className);
        }

        /// <summary>
        ///     Takes a string and separates with spaces between the Capital letters.
        /// </summary>
        /// <param name="pascalCaseString"></param>
        /// <returns></returns>
        private string GetUpperCaseString(string pascalCaseString)
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(pascalCaseString))
            {
                foreach (char c in pascalCaseString)
                {
                    if (char.IsUpper(c))
                        sb.Append(' ');
                    sb.Append(c);
                }
            }

            return sb.ToString().Trim();
        }

        private bool ShowNode(XmlNode node, bool includeSystemNodes)
        {
            return (includeSystemNodes ||
                    !XmlUtil.GetAttribute("type", node, true)
                        .StartsWith("Sitecore", StringComparison.InvariantCultureIgnoreCase));
        }

        private XmlNode GetAgentNode(string type)
        {
            return Factory.GetConfigNode(string.Format("scheduling/agent[@type='{0}']", type));
        }

        #endregion
    }
}