namespace EimzaSignerService
{
    /// <summary>
    /// PDF belgesi üzerindeki standart imza konumlarını tanımlar.
    /// </summary>
    public enum SignaturePosition // "class" kelimesini "enum" olarak değiştirmiş olduk.
    {
        /// <summary>
        /// Sayfanın sağ alt köşesi.
        /// </summary>
        BottomRight,

        /// <summary>
        /// Sayfanın sol alt köşesi.
        /// </summary>
        BottomLeft,

        /// <summary>
        /// Sayfanın sağ üst köşesi.
        /// </summary>
        TopRight,

        /// <summary>
        /// Sayfanın sol üst köşesi.
        /// </summary>
        TopLeft
    }
}