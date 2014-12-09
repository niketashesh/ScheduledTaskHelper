using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using Sitecore;
using Sitecore.Configuration;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Jobs;
using Sitecore.Reflection;
using Sitecore.Shell.Applications.Dialogs.ProgressBoxes;
using Sitecore.Tasks;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.Sheer;

namespace XCore.SharedSource.ScheduledTask
{
    public class ScheduledTaskHelper : BaseForm
    {
        // Fields
        protected Listview AgentTaskList;
        protected Listview ScheduledTaskList;
        protected Checkbox ShowSystemCB;

        // Methods
        [HandleMessage("XCore:execute")]
        protected void Execute(Message message)
        {
            if (!(AgentTaskList.SelectedItems.Any() || ScheduledTaskList.SelectedItems.Any()))
                Context.ClientPage.ClientResponse.Alert("Please select an task");
            else
            {
                List<XmlNode> agents =
                    AgentTaskList.SelectedItems.Select(task => GetAgentNode(task.Value)).ToList();
                List<Item> scheduledTasks =
                    ScheduledTaskList.SelectedItems.Select(task => Context.ContentDatabase.GetItem(task.Value)).ToList();
                if (agents.Any() || scheduledTasks.Any())
                {
                    ProgressBox.Execute("XCoreScheduledTaskHelper", "XCore Scheduled Task Helper", "", RunAgent,
                        "XCore:refresh", new object[] { agents, scheduledTasks });
                }
                else
                {
                    Context.ClientPage.ClientResponse.Alert("Cannot retreive tasks");
                }
            }
        }

        protected void FillTaskList()
        {
            XmlNodeList configNodes = Factory.GetConfigNodes("scheduling/agent");
            if (configNodes != null)
            {
                int count = configNodes.Count;
                for (int i = 0; i <= (count - 1); i++)
                {
                    XmlNode node = configNodes.Item(i);
                    if (ShowNode(node))
                    {
                        var item = new ListviewItem();
                        Context.ClientPage.AddControl(AgentTaskList, item);
                        item.ID = Control.GetUniqueID("I");
                        item.Icon = "";
                        item.ColumnValues["Name"] = item.Header = item.Value = node.Attributes["type"] == null ? Translate.Text("No type is defined") : node.Attributes["type"].Value;
                        item.ColumnValues["MethodName"] = node.Attributes["method"] == null ? Translate.Text("No method name is defined") : node.Attributes["method"].Value;
                        item.ColumnValues["Interval"] = node.Attributes["interval"] == null ? Translate.Text("No interval is defined") : node.Attributes["interval"].Value;
                    }
                }
            }
            string query = string.Format("/sitecore/*[@@id='{0}']//*[@@templateid='{1}']", ItemIDs.SystemRoot,
                TemplateIDs.Schedule);
            Item[] items = Context.ContentDatabase.SelectItems(query);
            if ((items != null) && items.Any())
            {
                foreach (Item item2 in items)
                {
                    var item3 = new ScheduleItem(item2);
                    if (item3 != null)
                    {
                        var item = new ListviewItem();
                        Context.ClientPage.AddControl(ScheduledTaskList, item);
                        item.ID = Control.GetUniqueID("I");
                        item.Icon = item3.Icon;
                        item.ColumnValues["Name"] = item.Header = item3.DisplayName;
                        item.ColumnValues["CommandName"] = (item3.CommandItem != null)
                            ? item3.CommandItem.DisplayName
                            : "No Command Item Defined";
                        item.ColumnValues["LastRun"] = item3.LastRun.ToString("MM/dd/yyyy HH:mm:ss");
                        item.Value = item3.ID.ToString();
                    }
                }
            }
        }

        private XmlNode GetAgentNode(string type)
        {
            return Factory.GetConfigNode(string.Format("scheduling/agent[@type='{0}']", type));
        }

        protected override void OnLoad(EventArgs args)
        {
            Assert.ArgumentNotNull(args, "e");
            base.OnLoad(args);
            if (!Context.ClientPage.IsEvent)
            {
                FillTaskList();
            }
        }

        [HandleMessage("XCore:refresh")]
        protected void Refresh(Message message)
        {
            AgentTaskList.Controls.Clear();
            ScheduledTaskList.Controls.Clear();
            FillTaskList();
            Context.ClientPage.ClientResponse.SetOuterHtml("AgentTaskList", AgentTaskList);
            Context.ClientPage.ClientResponse.SetOuterHtml("ScheduledTaskList", ScheduledTaskList);
        }

        protected void RunAgent(params object[] parameters)
        {
            if (parameters.Count() == 2)
            {
                var agentNodes = parameters[0] as List<XmlNode>;
                var scheduleItems = parameters[1] as List<Item>;
                if ((agentNodes != null) && (scheduleItems != null))
                {
                    RunAgent(agentNodes, scheduleItems);
                }
            }
        }

        protected void RunAgent(List<XmlNode> agentNodes, List<Item> scheduleItems)
        {
            Job.Total = agentNodes.Count + scheduleItems.Count;
            foreach (XmlNode node in agentNodes)
            {
                Job.AddMessage("Running {0}", new object[] { node.Attributes["type"] });
                if (node != null)
                {
                    object obj2 = ReflectionUtil.CreateObject(node);
                    string methodName = node.Attributes["method"].Value;
                    if (obj2 != null)
                    {
                        MethodInfo method = ReflectionUtil.GetMethod(obj2.GetType(), methodName, true, true, true,
                            new object[0]);
                        if (method != null)
                        {
                            ReflectionUtil.InvokeMethod(method, new object[0], obj2);
                        }
                    }
                }
                Job.Processed += 1L;
            }

            scheduleItems.Where(itm => itm.TemplateID == TemplateIDs.Schedule).ToList().ForEach(itm =>
            {
                var scheduledItem = new ScheduleItem(itm);
                Job.AddMessage("Running {0}", new object[] { scheduledItem.DisplayName });
                scheduledItem.Execute();
                Job.Processed += 1L;
            });
        }

        private bool ShowNode(XmlNode node)
        {
            return (ShowSystemCB.Checked ||
                    !node.Attributes["type"].Value.StartsWith("Sitecore", StringComparison.InvariantCultureIgnoreCase));
        }
    }
}