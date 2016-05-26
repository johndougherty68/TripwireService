using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
namespace TripwireService
{
    public class WitnessFileCreator
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static void Create(string fromPath, string toPath)
        {
            try
            {
                if (!System.IO.File.Exists(fromPath))
                {
                    throw new InvalidOperationException("File '" + fromPath + "' was not found.");
                }
                System.IO.File.Copy(fromPath, toPath, true);
            }
            catch (Exception ex)
            {
                throw ex;
            };
        }

        public static void Create(string toPath)
        {
            logger.Info("Creating WordWitnessFile at " + toPath);
            WordWitnessFile w = new WordWitnessFile();
            w.Create(toPath);
            logger.Info("Done Creating WordWitnessFile at " + toPath);
        }
    }
}
