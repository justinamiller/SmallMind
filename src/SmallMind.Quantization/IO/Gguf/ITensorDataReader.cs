namespace SmallMind.Quantization.IO.Gguf
{
    /// <summary>
    /// Interface for reading tensor data from GGUF files.
    /// Abstracts between stream-based and memory-mapped readers.
    /// </summary>
    public interface ITensorDataReader
    {
        /// <summary>
        /// Read raw tensor data from the GGUF file.
        /// </summary>
        /// <param name="offset">Offset in bytes from start of file</param>
        /// <param name="size">Size in bytes to read</param>
        /// <returns>Raw tensor data bytes</returns>
        byte[] ReadTensorData(ulong offset, ulong size);
    }
}
