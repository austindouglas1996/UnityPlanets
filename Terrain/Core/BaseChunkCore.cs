namespace UnityTerrainGenerator.Core
{
    /// <summary>
    /// Base class for chunk systems.
    /// Right now it just wraps the <see cref="IChunkConfiguration"/> so all chunks
    /// have access to their settings in a consistent way.
    /// 
    /// The intent is that this core can grow over time with other common state
    /// or utilities that every chunk system will need.
    /// </summary>
    public class BaseChunkCore
    {
        /// <summary>
        /// Create a new base core with a configuration.
        /// </summary>
        public BaseChunkCore(IChunkConfiguration configuration)
        {
            this.Configuration = configuration;
        }

        /// <summary>
        /// Configuration settings that control how chunks behave.
        /// </summary>
        public IChunkConfiguration Configuration
        {
            get { return _configuration; }
            set { _configuration = value; }
        }
        private IChunkConfiguration _configuration;
    }
}