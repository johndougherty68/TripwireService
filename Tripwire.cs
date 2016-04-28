using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//using System.Threading.Tasks;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
//using System.Drawing;
using System.IO;
using System.Management;
using System.Net;
using System.Net.Mail;
using Microsoft.Win32;
using System.ServiceProcess;
using NLog;
using System.Xml.Linq;

namespace TripwireService
{
    public class Tripwire
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();
        private string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        private string witnessFile="";
        private string witnessExt;
        private string tripFolderName = "#####TripFolder";
        private bool stopServerService;
        private bool stopVSSService;
        private bool shutdownServer;
        private List<string> foldersToWatch = new List<string>();
        private static SNSConfig snsconfig = new SNSConfig();
        private bool sendSNSAlert;
        private StringBuilder sb;

        public void DeleteTemps()
        //Delete temporary files created by application
        {
            if(this.foldersToWatch.Count()==0)
            {
                logger.Info("No tripwire folders exist.");
                return;
            }
            foreach (string s in this.foldersToWatch)
            {
                string destfolder = System.IO.Path.Combine(s, this.tripFolderName);
                logger.Info("Removing folder " + destfolder);
                if (!Directory.Exists(destfolder))
                {
                    continue;
                }

                System.IO.DirectoryInfo directory = new System.IO.DirectoryInfo(destfolder);
                foreach (System.IO.FileInfo file in directory.GetFiles())
                {
                    file.Delete();
                }

                foreach (System.IO.DirectoryInfo subDirectory in directory.GetDirectories())
                {
                    subDirectory.Delete(true);
                }
                System.Threading.Thread.Sleep(500);
                System.IO.Directory.Delete(destfolder, true);
                System.Threading.Thread.Sleep(500);
            }
            foldersToWatch.Clear();
        }

        private void FSWStop()
        //File system watcher stop
        {
            try
            {
                logger.Info("Shutting down file watchers");
                foreach (FileSystemWatcher watcher in watchers)
                {
                    logger.Info(watcher.Path);
                    watcher.Dispose();
                }
                logger.Info("Clearing watchers");
                watchers.Clear();
                logger.Info("File watchers shut down successfully");
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                throw;
            }
        }

        public void StopServerService()
        //Stops and disables lanmanserver service
        {
            try
            {
                logger.Info("Shutting down Server service");
                ServiceController sc = new ServiceController("Server");
                if (sc.Status.Equals(ServiceControllerStatus.Running))
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped);
                }
                RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\LanmanServer", true);
                key.SetValue("Start", 4);
                logger.Info("Server service shut down successfully");
            }
            catch(Exception ex)
            {
                logger.Error(ex.Message);
            }
        }

        public void StopVSSService()
        //Stops and disables volume shadow copy service
        {
            try
            {
                logger.Info("Shutting down Volume Shadow Copy service");
                ServiceController sc = new ServiceController("VSS");
                if (sc.Status.Equals(ServiceControllerStatus.Running))
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped);
                }
                RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\VSS", true);
                key.SetValue("Start", 4);
                logger.Info("Volume Shadow Copy service shut down successfully");
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
            }
        }

        public void ShutdownServer()
        //Force shuts down computer
        {
            try
            {
                logger.Info("Shutting down server (computer)");

                Process.Start("shutdown", "/f /s /t 00");

                logger.Info("Shutdown initiated successfully");
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
            }
        }

        public void Start()
        {
            try
            {
                //writeEvent("starting");
                foldersToWatch.Clear();
                logger.Info("Starting Watcher");
                logger.Info("Base Dir: " + AppDomain.CurrentDomain.BaseDirectory);
                string appConfigLoc = baseDir + "app.config";
                //writeEvent("app config location is " + appConfigLoc);
                logger.Info("Loading config info from " + appConfigLoc);
                if(!File.Exists(appConfigLoc))
                {
                    throw new FileNotFoundException("Config file " + appConfigLoc + " not found. Exiting.");
                }

                XDocument doc = XDocument.Load(appConfigLoc);

                // Get the appSettings element as a parent
                XContainer appSettings = doc.Element("configuration").Element("appSettings");

                // step through the "add" elements
                foreach (XElement xe in appSettings.Elements("add"))
                {
                    // Get the values
                    string addKey = xe.Attribute("key").Value;
                    string addValue = xe.Attribute("value").Value;

                    switch(addKey.ToLower())
                    {
                        case "folder":
                            foldersToWatch.Add(addValue);
                            break;
                        case "tripfoldername":
                            logger.Info("Setting trip folder name to " + addValue);
                            this.tripFolderName = addValue;
                            break;
                        case "witnessfile":
                            //no value for witnessfile - we'll create one later
                            if (addValue=="")
                            {
                                break;
                            }
                            
                            if(!File.Exists(addValue))
                            {
                                throw new FileNotFoundException("Witness file '" + addValue + "' does not exist.");
                            }
                            logger.Info("Setting witness file to " + addValue);
                            this.witnessFile = addValue;
                            break;
                        case "stopserverservice":
                            logger.Info("Setting value of " + addKey + " to " + addValue.ToString().ToLower());
                            this.stopServerService = (addValue.ToString().ToLower() == "true") ? true : false;
                            break;
                        case "stopvssservice":
                            logger.Info("Setting value of " + addKey + " to " + addValue.ToString().ToLower());
                            this.stopVSSService = (addValue.ToString().ToLower() == "true") ? true : false;
                            break;
                        case "shutdownserver":
                            logger.Info("Setting value of " + addKey + " to " + addValue.ToString().ToLower());
                            this.shutdownServer = (addValue.ToString().ToLower() == "true") ? true : false;
                            break;
                        case "sendsns":
                            logger.Info("Setting value of " + addKey + " to " + addValue.ToString().ToLower());
                            this.sendSNSAlert = (addValue.ToString().ToLower() == "true") ? true : false;
                            break;
                        case "snsarn":
                            logger.Info("Setting value of " + addKey + " to " + addValue.ToString().ToLower());
                            snsconfig.SNSArn = addValue;
                            break;
                        case "snskey":
                            logger.Info("Setting value of " + addKey + " to " + addValue.ToString().ToLower());
                            snsconfig.AccessKeyID = addValue;
                            break;
                        case "snsid":
                            logger.Info("Setting value of " + addKey + " to " + addValue.ToString().ToLower());
                            snsconfig.SecretAccessKey = addValue;
                            break;
                    }
                }

                //If there's no witness file specified, mention it in the log
                if(this.witnessFile=="")
                {
                    logger.Info("No witness file specified. One will be created automatically.");
                }

                //Now that we have all the configs, create the witness folder/files in the folders to watch
                foreach (string s in foldersToWatch)
                {
                    logger.Info("Adding watcher to " + s);

                    if (this.witnessFile == "")
                    {
                        witnessExt = "txt";
                        //create a file in memory
                        sb = new StringBuilder();
                        sb.AppendLine("Some text in the witness file");
                    }
                    else
                    {
                        witnessExt = Path.GetExtension(this.witnessFile);
                    }

                    string destfolder = System.IO.Path.Combine(s, this.tripFolderName);
                    string destfile = System.IO.Path.Combine(destfolder, "###_#" + witnessExt);
                    if (Directory.Exists(destfolder))
                    {
                        //do nothing                    
                    }
                    else
                    {
                        System.IO.Directory.CreateDirectory(destfolder);
                    }
                    if (File.Exists(destfile))
                    {
                        System.IO.File.Delete(destfile);
                        if (this.witnessFile == "")
                        {
                            //write the file in memory to the file in the folder
                            File.WriteAllText(destfile, sb.ToString());
                        }
                        else
                        {
                            System.IO.File.Copy(this.witnessFile, destfile, true);
                        }
                        File.SetAttributes(destfile, FileAttributes.Hidden);
                    }
                    else
                    {
                        if (this.witnessFile == "")
                        {
                            //write the file in memory to the file in the folder
                            File.WriteAllText(destfile, sb.ToString());
                        }
                        else
                        {
                            System.IO.File.Copy(this.witnessFile, destfile, true);
                        }
                        File.SetAttributes(destfile, FileAttributes.Hidden);
                    }

                    File.SetAttributes(destfolder, FileAttributes.Hidden);
                    FileSystemWatcher fsw = new FileSystemWatcher();
                    fsw.Path = destfolder;
                    fsw.IncludeSubdirectories = true;
                    fsw.Filter = "*.*";
                    fsw.NotifyFilter = System.IO.NotifyFilters.LastWrite | System.IO.NotifyFilters.LastAccess | System.IO.NotifyFilters.Attributes | System.IO.NotifyFilters.FileName | System.IO.NotifyFilters.DirectoryName;
                    fsw.Changed += fileSystemWatcher_Changed;
                    fsw.Renamed += fileSystemWatcher_Renamed;
                    fsw.Deleted += fileSystemWatcher_Deleted;
                    fsw.EnableRaisingEvents = true;
                    watchers.Add(fsw);
                    logger.Info("Watcher added to " + s);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                throw;
            }
        }


        //private void writeEvent(string message)
        //{
        //    System.Diagnostics.EventLog appLog = new System.Diagnostics.EventLog();
        //    appLog.Source = "CryptoWatcher";
            
        //    appLog.WriteEntry(message);
        //}
        
        public void Stop()
        {
            logger.Info("Stopping Watcher");
            FSWStop();
            DeleteTemps();
            logger.Info("Watcher Stopped");
        }


        void fileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            handleEvent(e);
        }

        void fileSystemWatcher_Renamed(object sender, FileSystemEventArgs e)
        {
            handleEvent(e);
        }

        void fileSystemWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            handleEvent(e);
        }

        private void handleEvent(FileSystemEventArgs e)
        {
            try
            {
                logEvent(e);
                if (stopServerService) { StopServerService(); }
                if (stopVSSService) { StopVSSService(); }
                if (shutdownServer) { ShutdownServer(); }
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);

                throw;
            }
        }

        private void logEvent(FileSystemEventArgs e)
        {
            try
            {
                string host = Dns.GetHostName();
                string message = "CryptoWatcher tripped in " + e.FullPath;
                message += " on " + host;
                message += "  Event: " + e.ChangeType;
                logger.Warn(message);

                if (sendSNSAlert)
                {
                    AWSSNSLogger sns = new AWSSNSLogger();
                    sns.TopicARN = snsconfig.SNSArn;
                    sns.AWSKey = snsconfig.AccessKeyID;
                    sns.SecretKey = snsconfig.SecretAccessKey;
                    sns.Write(message);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                throw;
            }
        }

    }
}
