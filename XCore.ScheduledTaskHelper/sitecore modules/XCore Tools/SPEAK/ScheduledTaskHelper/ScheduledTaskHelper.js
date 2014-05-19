define(["sitecore"], function(Sitecore) {
    var ScheduledTaskHelper = Sitecore.Definitions.App.extend({
        initialized: function() {
            app = this;
            //populate agent list
            this.getAgentsList(this.ShowSystemTasksCheckBox.viewModel.isChecked());
            this.AgentListDataSource.on("change:data", function() {
                var data = this.AgentListDataSource.get("data");
                this.AgentsList.set("items", data);
            }, this);
            this.RunButton.on("click", function() {
                var selectedTask = this.AgentsList.get("selectedItem");
                if (selectedTask === undefined || selectedTask == "")
                    alert("Please select a task");
                else
                    this.runAgent(selectedTask.attributes["TaskName"]);
            }, this);
            this.ShowSystemTasksCheckBox.on("change:isChecked", function() {
                this.getAgentsList(this.ShowSystemTasksCheckBox.viewModel.isChecked());
            }, this);

            //populate task list
            this.getTasksList();
            this.TaskListDataSource.on("change:data", function() {
                var data = this.TaskListDataSource.get("data");
                this.TaskList.set("items", data);
            }, this);

            this.ExecuteButton.on("click", function() {
                var selectedTask = this.TaskList.get("selectedItemId");
                if (selectedTask === undefined || selectedTask == "")
                    alert("Please select a task");
                else
                    this.runTask(selectedTask);
            }, this);
        },
        getAgentsList: function(includeSystemNodes) {
            var requestOptions = {
                onSuccess: this.getAgentsListCallback,
                url: "/api/sitecore/ScheduledTask/GetScheduledAgents?includeSystemNodes=" + includeSystemNodes
            };
            this.AgentListDataSource.viewModel.getData(requestOptions);
        },
        getAgentsListCallback: function() {
        },
        runAgent: function(agentType) {
            this.AgentsListProgressIndicator.viewModel.show();
            $.ajax({
                url: "/api/sitecore/ScheduledTask/RunAgent?type=" + agentType,
                success: this.runAgentCallback
            });
        },
        runAgentCallback: function(data) {
            app.AgentsListProgressIndicator.viewModel.hide();
            alert(data.Message);
        },
        getTasksList: function() {
            var requestOptions = {
                onSuccess: this.getTaskssListCallback,
                url: "/api/sitecore/ScheduledTask/GetScheduledTasks"
            };
            this.TaskListDataSource.viewModel.getData(requestOptions);
        },
        runTask: function(itemid) {
            this.TaskListProgressIndicator.viewModel.show();
            $.ajax({
                url: "/api/sitecore/ScheduledTask/RunTask?itemid=" + itemid,
                success: this.runTaskCallback
            });
        },
        runTaskCallback: function(data) {
            app.TaskListProgressIndicator.viewModel.hide();
            alert(data.Message);
        }


    });

    return ScheduledTaskHelper;
});