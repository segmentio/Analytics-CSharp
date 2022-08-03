/*
 * This file here is only for unit tests, thanks to C#,
 * a language designed with no unit testing in mind, that
 * makes mocking/spying unit tests so hard, and then blames
 * the developers not writing testable code. Creating a bunch
 * of useless interfaces and constructors and abusing the factory
 * pattern just for the purpose of tests is simply a bad idea.
 */

using System.Threading.Tasks;
using Segment.Analytics.Utilities;
using Segment.Concurrent;
using Segment.Sovran;

namespace Segment.Analytics
{
    public partial class Analytics
    {
        internal Analytics(Configuration configuration,
            Timeline timeline = null,
            Store store = null,
            Storage storage = null,
            Scope analyticsScope = null, 
            IDispatcher fileIODispatcher = null,
            IDispatcher networkIODispatcher = null,
            IDispatcher analyticsDispatcher = null,
            HTTPClient httpClient = null
            )
        {
            this.configuration = configuration;
            this.analyticsScope = analyticsScope ?? new Scope();
            IDispatcher dispatcher = new SynchronizeDispatcher();
            this.fileIODispatcher = fileIODispatcher ?? dispatcher;
            this.networkIODispatcher = networkIODispatcher ?? dispatcher;
            this.analyticsDispatcher = analyticsDispatcher ?? dispatcher;
            this.store = store ?? new Store(true);
            this.storage = storage ?? new Storage(this.store, configuration.writeKey, configuration.persistentDataPath, this.fileIODispatcher);
            this.timeline = timeline ?? new Timeline();
            
            Startup(httpClient);
        }
    }
}