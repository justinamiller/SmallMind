namespace SmallMind.Runtime.PretrainedModels
{
    /// <summary>
    /// Defines domain-specific specializations for pre-trained models.
    /// </summary>
    public enum DomainType
    {
        /// <summary>
        /// General-purpose domain (no specific specialization).
        /// </summary>
        General,

        /// <summary>
        /// Financial domain (stocks, market analysis, financial news).
        /// </summary>
        Finance,

        /// <summary>
        /// Healthcare domain (medical records, health articles).
        /// </summary>
        Healthcare,

        /// <summary>
        /// Legal domain (contracts, legal documents).
        /// </summary>
        Legal,

        /// <summary>
        /// E-commerce domain (product reviews, shopping trends).
        /// </summary>
        ECommerce
    }
}
