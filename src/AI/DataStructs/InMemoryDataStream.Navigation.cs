using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace AI.DataStructs;

public partial class InMemoryDataStream
{
    #region Управление
    /// <summary>
    /// Zips data in the stream
    /// </summary>
    /// <returns></returns>
    public InMemoryDataStream Zip()
    {
        if (IsZipped)
        {
            return this;
        }

        using (MemoryStream memory = new MemoryStream())
        {
            using (GZipStream tinyStream = new GZipStream(memory, CompressionLevel.Optimal))
            {
                using MemoryStream ms = ToMemoryStream();
                ms.CopyTo(tinyStream);
            }
            byte[] bytes = memory.ToArray();
            InitFromBytes(bytes, IsForWriting, IsEncrypted, true);
        }

        return this;
    }
    /// <summary>
    /// Unzips data in the stream
    /// </summary>
    /// <returns></returns>
    public InMemoryDataStream UnZip()
    {
        if (!IsZipped)
        {
            return this;
        }

        using (MemoryStream ms = ToMemoryStream())
        {
            using MemoryStream memory = new MemoryStream();
            using GZipStream decompres = new GZipStream(ms, CompressionMode.Decompress);
            decompres.CopyTo(memory);
            byte[] b = memory.ToArray();

            InitFromBytes(b, IsForWriting, IsEncrypted, false);
        }

        return this;
    }
    /// <summary>
    /// Encrypts data in the stream
    /// </summary>
    /// <param name="password"></param>
    /// <param name="salt"></param>
    public InMemoryDataStream Encrypt(string password, string salt = "AI Framework")
    {
        if (IsEncrypted)
        {
            return this;
        }

        byte[] key = GenKey(password, salt);
        byte[] dat = AsByteArray();
        dat = EncryptAes(dat, key);
        InitFromBytes(dat, IsForWriting, true, IsZipped);

        return this;
    }
    /// <summary>
    /// Decrypts data in the stream
    /// </summary>
    /// <param name="password"></param>
    /// <param name="salt"></param>
    public InMemoryDataStream Decrypt(string password, string salt = "AI Framework")
    {
        if (!IsEncrypted)
        {
            return this;
        }

        byte[] key = GenKey(password, salt);
        byte[] dat = AsByteArray();

        try
        {
            dat = DecryptAes(dat, key);
            InitFromBytes(dat, IsForWriting, false, IsZipped);
        }
        catch
        {
            throw new ArgumentException("Password is incorrect", nameof(password));
        }

        return this;
    }
    /// <summary>
    /// Returns data as a byte array
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] AsByteArray()
    {
        return _data;
    }
    /// <summary>
    /// Returns data as a memory stream
    /// </summary>
    /// <returns></returns>
    public MemoryStream ToMemoryStream()
    {
        MemoryStream memoryStream = new MemoryStream(_data);
        return memoryStream;
    }
    /// <summary>
    /// Сохранениеs data to the file
    /// </summary>
    /// <param name="path">Путь до файла</param>
    public void Save(string path)
    {
        if (_data.Length == 0)
        {
            throw new InvalidOperationException("Data is empty");
        }

        using FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        Save(fs);
    }
    /// <summary>
    /// Сохранениеs data to the System.IO.Stream
    /// </summary>
    /// <param name="stream">Поток</param>
    public void Save(Stream stream)
    {
        if (_data.Length == 0)
        {
            throw new InvalidOperationException("Data is empty");
        }

        stream.Write(_data, 0, _data.Length);
    }
    /// <summary>
    /// Returns data as base64 string
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return Convert.ToBase64String(AsByteArray());
    }
    #endregion

    #region Статические методы инициализации
    /// <summary>
    /// Initialize empty DataStream for writing data
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InMemoryDataStream Create()
    {
        return new InMemoryDataStream();
    }
    /// <summary>
    /// Inintialize DataStream for reading data from file
    /// </summary>
    /// <param name="path">Путь до файла</param>
    /// <param name="isEncrypted"></param>
    /// <param name="isZipped"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InMemoryDataStream FromFile(string path, bool isEncrypted = false, bool isZipped = false)
    {
        return new InMemoryDataStream(path, isEncrypted, isZipped);
    }
    /// <summary>
    /// Inintialize DataStream for reading data from System.IO.Stream
    /// </summary>
    /// <param name="stream">Поток</param>
    /// <param name="isEncrypted"></param>
    /// <param name="isZipped"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InMemoryDataStream FromSystemStream(Stream stream, bool isEncrypted = false, bool isZipped = false)
    {
        return new InMemoryDataStream(stream, isEncrypted, isZipped);
    }
    /// <summary>
    /// Inintialize DataStream for reading data from byte array
    /// </summary>
    /// <param name="data"></param>
    /// <param name="isEncrypted"></param>
    /// <param name="isZipped"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InMemoryDataStream FromByteArray(byte[] data, bool isEncrypted = false, bool isZipped = false)
    {
        return new InMemoryDataStream(data, isEncrypted, isZipped);
    }
    /// <summary>
    /// Initialize DataStream from base64 string for reading data
    /// </summary>
    /// <param name="strBase64"></param>
    /// <param name="isEncrypted"></param>
    /// <param name="isZipped"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InMemoryDataStream FromBase64String(string strBase64, bool isEncrypted = false, bool isZipped = false)
    {
        if (strBase64 == null)
        {
            throw new ArgumentNullException(nameof(strBase64));
        }

        byte[] array = Convert.FromBase64String(strBase64);
        return new InMemoryDataStream(array, isEncrypted, isZipped);
    }
    #endregion

    #region Приватные методы
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InitFromBytes(byte[] data, bool isForWriting = false, bool isEncrypted = false, bool isZipped = false)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        if (data.Length == 0)
        {
            throw new ArgumentException("Data is empty", nameof(data));
        }

        IsForWriting = isForWriting;
        IsForReading = !isForWriting;
        IsEncrypted = isEncrypted;
        IsZipped = isZipped;

        _data = data;
    }
    // Дописывание данных
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteInternal(byte[] array)
    {
        if (array == null)
        {
            throw new ArgumentNullException(nameof(array));
        }

        if (IsForReading)
        {
            throw new InvalidOperationException("Stream is opened for reading");
        }

        if (IsZipped)
        {
            throw new InvalidOperationException("Data is zipped");
        }

        if (IsEncrypted)
        {
            throw new InvalidOperationException("Data is encrypted");
        }

        byte[] newData = new byte[_data.Length + array.Length];
        Array.Copy(_data, newData, _data.Length);
        Array.Copy(array, 0, newData, _position, array.Length);
        _data = newData;
        _position += array.Length;
    }

    // Проверка параметров для чтения
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte[] ReadInternal(int count)
    {
        if (count <= 0)
        {
            throw new ArgumentException("Count can't be less or equal zero", nameof(count));
        }

        if (_position >= _data.Length - 1)
        {
            throw new InvalidOperationException("The end of stream was reached");
        }

        if (_position + count > _data.Length)
        {
            throw new ArgumentException("Too large count to read", nameof(count));
        }

        if (IsForWriting)
        {
            throw new InvalidOperationException("Stream is opened for writing");
        }

        if (IsZipped)
        {
            throw new InvalidOperationException("Data is zipped");
        }

        if (IsEncrypted)
        {
            throw new InvalidOperationException("Data is encrypted");
        }

        byte[] array = new byte[count];
        Array.Copy(_data, _position, array, 0, count);
        _position += count;
        return array;
    }

    // Генерация ключа
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte[] GenKey(string pass, string salt)
    {
        Rfc2898DeriveBytes rfc = new Rfc2898DeriveBytes(pass, Encoding.ASCII.GetBytes(salt));
        return rfc.GetBytes(32);
    }

    //Кодер
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] EncryptAes(byte[] data, byte[] key)
    {
        string plainText = Convert.ToBase64String(data);

        byte[] encrypted;

        using (Aes aesAlg = Aes.Create())
        {
            aesAlg.Key = key;
            aesAlg.IV = IV;

            ICryptoTransform cryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

            using MemoryStream msEncrypt = new MemoryStream();
            using CryptoStream csEncrypt = new CryptoStream(msEncrypt, cryptor, CryptoStreamMode.Write);
            using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
            {
                swEncrypt.Write(plainText);
            }
            encrypted = msEncrypt.ToArray();
        }
        return encrypted;
    }

    //Декодер
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] DecryptAes(byte[] cipherText, byte[] key)
    {
#pragma warning disable CS8600 // Преобразование литерала, допускающего значение NULL или возможного значения NULL в тип, не допускающий значение NULL.
        string base64 = null;
#pragma warning restore CS8600 // Преобразование литерала, допускающего значение NULL или возможного значения NULL в тип, не допускающий значение NULL.

        using (Aes aesAlg = Aes.Create())
        {
            aesAlg.Key = key;
            aesAlg.IV = IV;

            ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

            using MemoryStream msDecrypt = new MemoryStream(cipherText);
            using CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using StreamReader srDecrypt = new StreamReader(csDecrypt);
            base64 = srDecrypt.ReadToEnd();
        }

        return Convert.FromBase64String(base64);
    }
    #endregion
}
