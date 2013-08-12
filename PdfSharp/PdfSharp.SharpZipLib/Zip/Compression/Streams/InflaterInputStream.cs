// InflaterInputStream.cs
//
// Copyright (C) 2001 Mike Krueger
// Copyright (C) 2004 John Reilly
//
// This file was translated from java, it was part of the GNU Classpath
// Copyright (C) 2001 Free Software Foundation, Inc.
//
// This program is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either version 2
// of the License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
//
// Linking this library statically or dynamically with other modules is
// making a combined work based on this library.  Thus, the terms and
// conditions of the GNU General Public License cover the whole
// combination.
// 
// As a special exception, the copyright holders of this library give you
// permission to link this library with independent modules to produce an
// executable, regardless of the license terms of these independent
// modules, and to copy and distribute the resulting executable under
// terms of your choice, provided that you also meet, for each linked
// independent module, the terms and conditions of the license of that
// module.  An independent module is a module which is not derived from
// or based on this library.  If you modify this library, you may extend
// this exception to your version of the library, but you are not
// obligated to do so.  If you do not wish to do so, delete this
// exception statement from your version.

using System;
using System.IO;

using PdfSharp.SharpZipLib.Zip.Compression;
using PdfSharp.SharpZipLib.Checksums;

namespace PdfSharp.SharpZipLib.Zip.Compression.Streams
{

  /// <summary>
  /// This filter stream is used to decompress data compressed using the "deflate"
  /// format. The "deflate" format is described in RFC 1951.
  ///
  /// Author of the original java version: John Leuner.
  /// </summary>
  internal class InflaterInputStream : Stream
  {
    /// <summary>
    /// Decompressor for this stream
    /// </summary>
    protected Inflater inf;

    /// <summary>
    /// Byte array used for buffering input.
    /// </summary>
    protected byte[] buf;

    /// <summary>
    /// Size of buffer <see cref="buf"></see>
    /// </summary>
    protected int len;

    // Used for reading single bytes the ReadByte() call
    private byte[] onebytebuffer = new byte[1];

    /// <summary>
    /// Base stream the inflater reads from.
    /// </summary>
    protected Stream baseInputStream;

    ////		/// <summary>
    ////		/// The compressed size
    ////		/// </summary>
    ////		protected long csize;

    bool isStreamOwner = true;

    /// <summary>
    /// Get/set flag indicating ownership of underlying stream.
    /// When the flag is true <see cref="Close"/> will close the underlying stream also.
    /// </summary>
    /// <remarks>
    /// The default value is true.
    /// </remarks>
    public bool IsStreamOwner
    {
      get { return isStreamOwner; }
      set { isStreamOwner = value; }
    }

    /// <summary>
    /// Gets a value indicating whether the current stream supports reading
    /// </summary>
    public override bool CanRead
    {
      get
      {
        return baseInputStream.CanRead;
      }
    }

    /// <summary>
    /// Gets a value of false indicating seeking is not supported for this stream.
    /// </summary>
    public override bool CanSeek
    {
      get
      {
        return false;
      }
    }

    /// <summary>
    /// Gets a value of false indicating that this stream is not writeable.
    /// </summary>
    public override bool CanWrite
    {
      get
      {
        return false;
      }
    }

    /// <summary>
    /// A value representing the length of the stream in bytes.
    /// </summary>
    public override long Length
    {
      get
      {
        return len;
      }
    }

    /// <summary>
    /// The current position within the stream.
    /// Throws a NotSupportedException when attempting to set the position
    /// </summary>
    /// <exception cref="NotSupportedException">Attempting to set the position</exception>
    public override long Position
    {
      get
      {
        return baseInputStream.Position;
      }
      set
      {
        throw new NotSupportedException("InflaterInputStream Position not supported");
      }
    }

    /// <summary>
    /// Flushes the baseInputStream
    /// </summary>
    public override void Flush()
    {
      baseInputStream.Flush();
    }

    /// <summary>
    /// Sets the position within the current stream
    /// Always throws a NotSupportedException
    /// </summary>
    /// <exception cref="NotSupportedException">Any access</exception>
    public override long Seek(long offset, SeekOrigin origin)
    {
      throw new NotSupportedException("Seek not supported");
    }

    /// <summary>
    /// Set the length of the current stream
    /// Always throws a NotSupportedException
    /// </summary>
    /// <exception cref="NotSupportedException">Any access</exception>
    public override void SetLength(long val)
    {
      throw new NotSupportedException("InflaterInputStream SetLength not supported");
    }

    /// <summary>
    /// Writes a sequence of bytes to stream and advances the current position
    /// This method always throws a NotSupportedException
    /// </summary>
    /// <exception cref="NotSupportedException">Any access</exception>
    public override void Write(byte[] array, int offset, int count)
    {
      throw new NotSupportedException("InflaterInputStream Write not supported");
    }

    /// <summary>
    /// Writes one byte to the current stream and advances the current position
    /// Always throws a NotSupportedException
    /// </summary>
    /// <exception cref="NotSupportedException">Any access</exception>
    public override void WriteByte(byte val)
    {
      throw new NotSupportedException("InflaterInputStream WriteByte not supported");
    }

    /// <summary>
    /// Entry point to begin an asynchronous write.  Always throws a NotSupportedException.
    /// </summary>
    /// <param name="buffer">The buffer to write data from</param>
    /// <param name="offset">Offset of first byte to write</param>
    /// <param name="count">The maximum number of bytes to write</param>
    /// <param name="callback">The method to be called when the asynchronous write operation is completed</param>
    /// <param name="state">A user-provided object that distinguishes this particular asynchronous write request from other requests</param>
    /// <returns>An <see cref="System.IAsyncResult">IAsyncResult</see> that references the asynchronous write</returns>
    /// <exception cref="NotSupportedException">Any access</exception>
    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
    {
      throw new NotSupportedException("InflaterInputStream BeginWrite not supported");
    }

    /// <summary>
    /// Create an InflaterInputStream with the default decompressor
    /// and a default buffer size of 4KB.
    /// </summary>
    /// <param name = "baseInputStream">
    /// The InputStream to read bytes from
    /// </param>
    public InflaterInputStream(Stream baseInputStream)
      : this(baseInputStream, new Inflater(), 4096)
    {
    }

    /// <summary>
    /// Create an InflaterInputStream with the specified decompressor
    /// and a default buffer size of 4KB.
    /// </summary>
    /// <param name = "baseInputStream">
    /// The source of input data
    /// </param>
    /// <param name = "inf">
    /// The decompressor used to decompress data read from baseInputStream
    /// </param>
    public InflaterInputStream(Stream baseInputStream, Inflater inf)
      : this(baseInputStream, inf, 4096)
    {
    }

    /// <summary>
    /// Create an InflaterInputStream with the specified decompressor
    /// and the specified buffer size.
    /// </summary>
    /// <param name = "baseInputStream">
    /// The InputStream to read bytes from
    /// </param>
    /// <param name = "inflater">
    /// The decompressor to use
    /// </param>
    /// <param name = "bufferSize">
    /// Size of the buffer to use
    /// </param>
    public InflaterInputStream(Stream baseInputStream, Inflater inflater, int bufferSize)
    {
      if (baseInputStream == null)
      {
        throw new ArgumentNullException("InflaterInputStream baseInputStream is null");
      }

      if (inflater == null)
      {
        throw new ArgumentNullException("InflaterInputStream Inflater is null");
      }

      if (bufferSize <= 0)
      {
        throw new ArgumentOutOfRangeException("bufferSize");
      }

      this.baseInputStream = baseInputStream;
      this.inf = inflater;
      buf = new byte[bufferSize];

      if (baseInputStream.CanSeek)
      {
        this.len = (int)baseInputStream.Length;
      }
      else
      {
        this.len = 0;
      }
    }

    /// <summary>
    /// Returns 0 once the end of the stream (EOF) has been reached.
    /// Otherwise returns 1.
    /// </summary>
    public virtual int Available
    {
      get
      {
        return inf.IsFinished ? 0 : 1;
      }
    }

    /// <summary>
    /// Closes the input stream.  When <see cref="IsStreamOwner"></see>
    /// is true the underlying stream is also closed.
    /// </summary>
    public override void Close()
    {
      if (isStreamOwner)
      {
        baseInputStream.Close();
      }
    }

    int readChunkSize = 0;

    /// <summary>
    /// Sets the size of chunks to read from the input stream
    /// 0 means as larger as possible.
    /// </summary>
    /// <remarks>
    /// Used to handle decryption where the length of stream is unknown.
    /// </remarks>
    protected int BufferReadSize
    {
      get
      {
        return readChunkSize;
      }

      set
      {
        readChunkSize = value;
      }
    }

    /// <summary>
    /// Fill input buffer with a chunk of data.
    /// </summary>		
    protected void FillInputBuffer()
    {
      if (readChunkSize <= 0)
      {
        len = baseInputStream.Read(buf, 0, buf.Length);
      }
      else
      {
        len = baseInputStream.Read(buf, 0, readChunkSize);
      }

    }
    /// <summary>
    /// Fills the buffer with more data to decompress.
    /// </summary>
    /// <exception cref="SharpZipBaseException">
    /// Stream ends early
    /// </exception>
    protected void Fill()
    {
      FillInputBuffer();

      if (keys != null)
      {
        DecryptBlock(buf, 0, len);
      }

#if READ_SINGLE_WHEN_DECRYPTING
			// This solves some decryption problems but there are still some lurking.
			// At issue is exactly where the stream and decryption should finish.
			if (keys == null) {
				len = baseInputStream.Read(buf, 0, buf.Length);
			} else {
				len = baseInputStream.Read(buf, 0, 1);
			}
			
			if (keys != null) {
				DecryptBlock(buf, 0, len);
			}
#endif

#if STANDARD
			len = baseInputStream.Read(buf, 0, buf.Length);
			
			if (keys != null) {
				DecryptBlock(buf, 0, System.Math.Min((int)(csize - inf.TotalIn), len));
			}
#endif

      if (len <= 0)
      {
        throw new SharpZipBaseException("Deflated stream ends early.");
      }

      inf.SetInput(buf, 0, len);
    }

    /// <summary>
    /// Reads one byte of decompressed data.
    ///
    /// The byte is baseInputStream the lower 8 bits of the int.
    /// </summary>
    /// <returns>
    /// The byte read cast to an int, or -1 on end of stream.
    /// </returns>
    public override int ReadByte()
    {
      int nread = Read(onebytebuffer, 0, 1); // read one byte
      if (nread > 0)
      {
        return onebytebuffer[0] & 0xff;
      }
      return -1; // ok
    }

    /// <summary>
    /// Decompresses data into the byte array
    /// </summary>
    /// <param name ="b">
    /// The array to read and decompress data into
    /// </param>
    /// <param name ="off">
    /// The offset indicating where the data should be placed
    /// </param>
    /// <param name ="len">
    /// The number of bytes to decompress
    /// </param>
    /// <returns>The number of bytes read.  Zero signals the end of stream</returns>
    /// <exception cref="SharpZipBaseException">
    /// Inflater needs a dictionary
    /// </exception>
    public override int Read(byte[] b, int off, int len)
    {
      for (; ; )
      {
        int count;
        try
        {
          count = inf.Inflate(b, off, len);
        }
        catch (Exception e)
        {
          throw new SharpZipBaseException(e.ToString());
        }

        if (count > 0)
        {
          return count;
        }

        if (inf.IsNeedingDictionary)
        {
          throw new SharpZipBaseException("Need a dictionary");
        }
        else if (inf.IsFinished)
        {
          return 0;
        }
        else if (inf.IsNeedingInput)
        {
          Fill();
        }
        else
        {
          throw new InvalidOperationException("Don't know what to do");
        }
      }
    }

    /// <summary>
    /// Skip specified number of bytes of uncompressed data
    /// </summary>
    /// <param name ="n">
    /// Number of bytes to skip
    /// </param>
    /// <returns>
    /// The number of bytes skipped, zero if the end of 
    /// stream has been reached
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Number of bytes to skip is zero or less
    /// </exception>
    public long Skip(long n)
    {
      if (n <= 0)
      {
        throw new ArgumentOutOfRangeException("n");
      }

      // v0.80 Skip by seeking if underlying stream supports it...
      if (baseInputStream.CanSeek)
      {
        baseInputStream.Seek(n, SeekOrigin.Current);
        return n;
      }
      else
      {
        int len = 2048;
        if (n < len)
        {
          len = (int)n;
        }
        byte[] tmp = new byte[len];
        return (long)baseInputStream.Read(tmp, 0, tmp.Length);
      }
    }

    #region Encryption stuff

    // TODO  Refactor this code.  The presence of Zip specific code in this low level class is wrong

    /// <summary>
    /// A buffer used for decrypting data.  Used to hold Zip crypto header.
    /// </summary>
    protected byte[] cryptbuffer = null;

    uint[] keys = null;

    /// <summary>
    /// Decrypt a single byte
    /// </summary>
    /// <returns>plain text byte value</returns>
    protected byte DecryptByte()
    {
      uint temp = ((keys[2] & 0xFFFF) | 2);
      return (byte)((temp * (temp ^ 1)) >> 8);
    }

    /// <summary>
    /// Decrypt cipher text block, updating keys
    /// </summary>
    /// <param name="buf">Data to decrypt</param>
    /// <param name="off">Offset of first byte to process</param>
    /// <param name="len">Number of bytes to process</param>
    protected void DecryptBlock(byte[] buf, int off, int len)
    {
      for (int i = off; i < off + len; ++i)
      {
        buf[i] ^= DecryptByte();
        UpdateKeys(buf[i]);
      }
    }

    /// <summary>
    /// Initialise the decryption keys
    /// </summary>
    /// <param name="password">The password used to initialise the keys</param>
    protected void InitializePassword(string password)
    {
      keys = new uint[] {
				0x12345678,
				0x23456789,
				0x34567890
			};
      for (int i = 0; i < password.Length; ++i)
      {
        UpdateKeys((byte)password[i]);
      }
    }

    /// <summary>
    /// Update the decryption keys
    /// </summary>
    /// <param name="ch">Character to update the keys with</param>
    protected void UpdateKeys(byte ch)
    {
      keys[0] = Crc32.ComputeCrc32(keys[0], ch);
      keys[1] = keys[1] + (byte)keys[0];
      keys[1] = keys[1] * 134775813 + 1;
      keys[2] = Crc32.ComputeCrc32(keys[2], (byte)(keys[1] >> 24));
    }

    /// <summary>
    /// Clear any cryptographic state.
    /// </summary>		
    protected void StopDecrypting()
    {
      keys = null;
      cryptbuffer = null;
    }
    #endregion
  }
}
