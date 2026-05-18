using System;
using System.ServiceModel;
using Common;

namespace Client
{
    public class WcfServiceProxy : IDisposable
    {
        private readonly ChannelFactory<IWindTurbineService> factory;
        private readonly IWindTurbineService service;
        private bool disposed;

        public WcfServiceProxy(string endpointName)
        {
            factory = new ChannelFactory<IWindTurbineService>(endpointName);
            service = factory.CreateChannel();
        }

        public IWindTurbineService Service
        {
            get { return service; }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            IClientChannel channel = service as IClientChannel;

            try
            {
                if (channel != null)
                {
                    if (channel.State == CommunicationState.Faulted)
                    {
                        channel.Abort();
                    }
                    else
                    {
                        channel.Close();
                    }
                }

                if (factory.State == CommunicationState.Faulted)
                {
                    factory.Abort();
                }
                else
                {
                    factory.Close();
                }
            }
            catch
            {
                if (channel != null) channel.Abort();
                factory.Abort();
            }

            disposed = true;
        }
    }
}
