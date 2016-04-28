using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//using System.Threading.Tasks;
using Amazon.Runtime;
using NLog;
using NLog.Config;
using NLog.LayoutRenderers;
using NLog.Targets;
using Amazon;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

namespace TripwireService
{
    //[Target("AWSSNSLogger")]
    public class AWSSNSLogger//:TargetWithLayout
    {
        public AWSSNSLogger()
        {

        }

        [RequiredParameter]
        public string AWSKey { get; set; }

        [RequiredParameter]
        public string SecretKey { get; set; }

        [RequiredParameter]
        public string TopicARN { get; set; }

        public void Write(string logMessage)
        {
            try
            {
                // string logMessage = this.Layout.Render(logEvent);
                AmazonSimpleNotificationServiceConfig config = new AmazonSimpleNotificationServiceConfig();
                config.RegionEndpoint = RegionEndpoint.USEast1;
                AWSCredentials awsc = new BasicAWSCredentials(this.AWSKey, this.SecretKey);
                var sns = new AmazonSimpleNotificationServiceClient(awsc, config);
                sns.Publish(new PublishRequest
                {
                    Message = logMessage,
                    TopicArn = TopicARN
                });
            }
            catch (AmazonSimpleNotificationServiceException ex)
            {
                //Util.LogError(ex);
                throw ex;
            }
        }
    }
}
