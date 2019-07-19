//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using System.Windows;
//using System.Windows.Controls;
//using System.Windows.Data;
//using System.Windows.Documents;
//using System.Windows.Input;
//using System.Windows.Media;
//using System.Windows.Media.Imaging;
//using System.Windows.Navigation;
//using System.Windows.Shapes;

//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;

//using System;
//using System.Collections;
//using System.IO;
//using System.Net;
//using System.Net.Security;
//using System.Text;
//using Toggl.Services;
//using Toggl;
//using Toggl.Extensions;
//using Toggl.Interfaces;

//namespace MHTimer
//{
//    /// <summary>
//    /// MainWindow.xaml の相互作用ロジック
//    /// </summary>
//    public partial class ToggleTest : Window
//    {

//        public WorkspaceService WorkspaceService;
//        public ProjectService ProjectService;
//        public TimeEntryService TimeEntryService;

//        private IApiService ToggleSrv { get; set; }

//        int DefaultWorkspaceId;

//        public ToggleTest()
//        {
//            InitializeComponent();

//            //var apiKey = "2d1d95cef10e17831ec505e9e6f9f7ea";
//            //var usrSrv = new Toggl.Services.UserService(apiKey);
//            //var c = usrSrv.GetCurrent();
//            //Console.WriteLine(c.FullName);
//            //Console.WriteLine(c.Email);

//            var apiKey = "339dd60598d05c0c79608ba5adc562e4";
//            var t = new Toggl.Toggl(apiKey);
//            var c = t.User.GetCurrent();

//            var user = new UserService(apiKey);
//            Console.WriteLine(user.GetCurrent().Email);

//            ProjectService = new ProjectService(apiKey);
//            WorkspaceService = new WorkspaceService(apiKey);
//            TimeEntryService = new TimeEntryService(apiKey);

//            List<Workspace> workspaces = WorkspaceService.List();
//            List<Project> projects = ProjectService.List();
//            DefaultWorkspaceId = workspaces.First().Id.Value;

//            foreach (Project p in projects)
//            {
//                Console.WriteLine(p.Name);
//                Console.WriteLine(p.Id);
//            }

//            //var project = new Project
//            //{
//            //    IsBillable = true,
//            //    WorkspaceId = DefaultWorkspaceId,
//            //    Name = "TTTTT" + DateTime.UtcNow,
//            //    IsAutoEstimates = false
//            //};

//            //var act = ProjectService.Add(project);
//            TimeEntry te = new TimeEntry()
//            {
//                CreatedWith = "TogglAPI.Net",
//                Description = "Start a new task",
//                WorkspaceId = DefaultWorkspaceId
//            };

//            TimeEntry te2 = new TimeEntry()
//            {
//                IsBillable = true,
//                CreatedWith = "TogglAPI.Net",
//                Description = "ClipStudio Paint",
//                ProjectId = 150055033,
//                Duration = 1200,
//                Start = DateTime.Now.ToIsoDateStr(),
//                Stop = DateTime.Now.AddMinutes(10).ToIsoDateStr(),
//                WorkspaceId = DefaultWorkspaceId
//            };

//            //TimeEntry te3 = Start(te);

//            //TimeEntryService.Add(te2);


//            var entries = TimeEntryService.List();
//            Console.WriteLine(entries);

//            Console.WriteLine(c.FullName);
//            //Console.WriteLine(c.Email);

//            //var t = new Toggl.Toggl();
//            //var obj = t.Client.List();

//        }

//        public TimeEntry Start(TimeEntry obj)
//        {
//            var url = ApiRoutes.TimeEntry.TimeEntryStartUrl;

//            var timeEntry = ToggleSrv.Post(url, obj.ToJson()).GetData<TimeEntry>();

//            return timeEntry;
//        }
//    }
//}
