using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace ConsoleApp1
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var account = CloudStorageAccount.Parse("");
            var client = account.CreateCloudBlobClient();
            var container = client.GetContainerReference("test");

            ResponseListener.Initialize();

            while (true)
            {
                Console.WriteLine(Environment.NewLine + "Listing -- " + DateTime.Now.ToLongTimeString());

                BlobResultSegment segment = null;

                string clientRequestId = Guid.NewGuid().ToString();

                using (ResponseListener.TrackRequest(clientRequestId))
                {
                    CancellationTokenSource delayCts = new CancellationTokenSource();
                    OperationContext context = new OperationContext { ClientRequestID = clientRequestId };

                    Task delay = Task.Delay(TimeSpan.FromMinutes(1), delayCts.Token);
                    Task<BlobResultSegment> segmentTask = container.ListBlobsSegmentedAsync(prefix: null, useFlatBlobListing: true,
                       blobListingDetails: BlobListingDetails.None, maxResults: 5000, currentToken: null,
                       options: null, operationContext: context);

                    Task finishedTask = await Task.WhenAny(delay, segmentTask);

                    if (Equals(finishedTask, delay))
                    {
                        Console.WriteLine("Hang detected. Disposing response to unblock.");
                        ResponseListener.UnblockResponse(clientRequestId);

                        try
                        {
                            // Depending on where it was blocked, this may actually retry and succeed.
                            segment = await segmentTask;
                        }
                        catch (OperationCanceledException)
                        {
                        }
                    }
                    else
                    {
                        segment = await segmentTask;
                    }

                    delayCts.Cancel();
                    delayCts.Dispose();
                }

                Console.WriteLine("Listed  -- " + DateTime.Now.ToLongTimeString() + " -- " + segment?.Results?.Count());
            }
        }
    }
}
