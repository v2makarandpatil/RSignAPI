using Microsoft.Practices.Unity;

namespace eSign.Web.Helpers
{
    public class ServiceLocator
    {
        private static readonly ServiceLocator Instance;

        static ServiceLocator()
        {
            Instance = new ServiceLocator();
        }

        private ServiceLocator()
        {
        }

        /// <summary>
        /// Gets the singleton instance of the ServiceLocator
        /// </summary>
        public static ServiceLocator Current
        {
            get { return Instance; }
        }

        /// <summary>
        /// Gets or sets the unity Container.
        /// </summary>
        public IUnityContainer Container { get; set; }
    }
}