using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Novacode;
using NLog;

namespace TripwireService
{
    public class WordWitnessFile : IWitnessFile
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        public void Create(string path)
        {
            try
            {
                logger.Info("creating file at " + path);
                var doc = DocX.Create(path);
                doc.InsertParagraph("This is not the file you're looking for.");
                doc.InsertParagraph("You can go about your business.");
                doc.InsertParagraph("Move along.");
                doc.Save();
                logger.Info("done creating file at " + path);
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                throw ex;
            }
        }
    }
}
