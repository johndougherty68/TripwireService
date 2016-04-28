using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
//using System.Threading.Tasks;
using NLog;

namespace TripwireService
{
    public partial class CryptoWatcherService : ServiceBase
    {

        private TripwireService.Tripwire cw;
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public CryptoWatcherService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            logger.Info("Starting Watcher");
            cw = new TripwireService.Tripwire();
            cw.Start();
            logger.Info("Watcher started");
        }

        protected override void OnStop()
        {
            cw.Stop();
        }
    }
}
