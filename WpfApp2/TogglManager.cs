using Toggl.Services;
using Toggl;
using Toggl.Extensions;
using Toggl.Interfaces;
using System.Collections.Generic;
using Toggl.Services;
using Toggl;
using Toggl.Extensions;
using Toggl.Interfaces;
using System;
using System.Linq;
using System.Windows;

namespace WpfApp2
{

    public class TogglManager
    {
        private MainWindow mainWindow;

        public string ApiKey { get; set; } = "339dd60598d05c0c79608ba5adc562e4";
        string PreApiKey { get; set; } = "";
        public string User{ get; set; } = "";

        TimeEntryService timeEntryService;

        private int defaultWorkspaceID;

        //public List<(string name, int id)> ProjectIDs { get; private set; } 
        //    = new List<(string name, int id)>() { ("無し", 0) };
        public Dictionary<string, int> ProjectIDs { get; private set; } =
            new Dictionary<string, int>();
        public List<string> Tags { get; set; } = new List<string>() { "無し" };

        public TogglManager(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
            var t = new Toggl.Toggl(ApiKey);
        }

        public void Test()
        {

            TimeEntry te2 = new TimeEntry()
            {
                IsBillable = true,
                CreatedWith = "TogglAPI.Net",
                Description = "Test",
                ProjectId = 150055033,
                Duration = 1200,
                Start = DateTime.Now.ToIsoDateStr(),
                Stop = DateTime.Now.AddMinutes(10).ToIsoDateStr(),
                WorkspaceId = defaultWorkspaceID
            };

            timeEntryService.Add(te2);
        }

        public void Init()
        {
            try
            {
                if (!string.IsNullOrEmpty(ApiKey))
                {
                    var userService = new UserService(ApiKey);
                    User = userService.GetCurrent().Email;
                }

                var projectService = new ProjectService(ApiKey);
                List<Project> projects = projectService.List();
                foreach (Project p in projects)
                {
                    ProjectIDs.Add(p.Name, (int)p.Id);
                }

                timeEntryService = new TimeEntryService(ApiKey);

                var workspaceService = new WorkspaceService(ApiKey);
                List<Workspace> workspaces = workspaceService.List();
                defaultWorkspaceID = workspaces.First().Id.Value;
                var tags = workspaceService.Tags(defaultWorkspaceID);
                foreach (Tag t in tags)
                {
                    Tags.Add(t.Name);
                }

            }
            catch (Exception e)
            {
                throw e;
            }

        }



        public void SetAPIKey(string key)
        {
            try
            {
                ApiKey = key;
                Init();
            }
            catch (Exception e)
            {
                throw e;
            }

        }

        public void GetProjects()
        {

        }

        public void SetTimeEntry(AppDataObject appData)
        {
            int projectID = ProjectIDs[appData.LinkedProjectName];
            int duration = (int)(appData.LaunchedTime - appData.LaunchedTime).TotalSeconds;
            TimeEntry te = new TimeEntry()
            {
                IsBillable = true,
                CreatedWith = "TogglAPI.Net",
                Description = appData.DisplayedName,
                ProjectId = projectID,
                TagNames = new List<string>() { appData.LinkedTag },
                Duration = duration,
                Start = appData.LaunchedTime.ToIsoDateStr(),
                Stop = appData.LastTime.ToIsoDateStr(),
                WorkspaceId = defaultWorkspaceID
            };

            timeEntryService.Add(te);

        }

    }
}
