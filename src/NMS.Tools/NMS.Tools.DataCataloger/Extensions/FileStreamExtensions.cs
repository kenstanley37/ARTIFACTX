namespace NMS.Tools.DataCataloger.Extensions;

public static class FileStreamExtensions
{
    public static byte[] ReadBytes(this FileStream fs, int count)
    {
        byte[] buffer = new byte[count];
        int read = fs.Read(buffer, 0, count);

        if (read < count)
        {
            byte[] trimmed = new byte[read];
            Buffer.BlockCopy(buffer, 0, trimmed, 0, read);
            return trimmed;
        }

        return buffer;
    }

    public static long ReadInt64(this FileStream fs)
    {
        byte[] buffer = fs.ReadBytes(8);
        return BitConverter.ToInt64(buffer, 0);
    }

    public static int ReadInt32(this FileStream fs)
    {
        byte[] buffer = fs.ReadBytes(4);
        return BitConverter.ToInt32(buffer, 0);
    }

}
