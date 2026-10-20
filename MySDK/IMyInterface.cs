using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MySDK
{
    public interface IMyInterface
    {
        void Start(CancellationToken serviceStoppingToken);
        Task RunningTask { get; }

    }
}
