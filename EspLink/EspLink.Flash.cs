using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace EL
{
	partial class EspLink
    {
        const int ERASE_REGION_TIMEOUT_PER_MB = 60;
		async Task<uint> FlashBeginAsync(CancellationToken cancellationToken, uint size, uint compsize, uint offset, uint blockSize, int timeout = -1)
		{
			CheckReady();
			// Start downloading compressed data to Flash (performs an erase)
			// Returns number of blocks (size self.FLASH_WRITE_SIZE) to write.
			if (blockSize == 0)
			{
				blockSize = Device.FLASH_WRITE_SIZE;
			}
			var num_blocks = (compsize + blockSize - 1) / blockSize;
			var erase_size = size;
			int timeout2 = -1;
			if (timeout > -1)
			{
				if (IsStub)
				{
					timeout2 = DefaultTimeout;
				}
				else
				{
					timeout2 = 1000 * (int)(ERASE_REGION_TIMEOUT_PER_MB * ((float)size / 1e6));
				}
			}
			uint perase_size = erase_size;
			uint pnum_blocks = num_blocks;
			uint pblockSize = blockSize;
			uint poffset = offset;

			if (!BitConverter.IsLittleEndian)
			{
				perase_size = SwapBytes(perase_size);
				pnum_blocks = SwapBytes(pnum_blocks);
				pblockSize = SwapBytes(pblockSize);
				poffset = SwapBytes(poffset);
			}
			var data = new byte[Device.SUPPORTS_ENCRYPTED_FLASH ? 20 : 16];
			if (Device.SUPPORTS_ENCRYPTED_FLASH)
			{
				PackUInts(data, 0, new uint[] { perase_size, pnum_blocks, pblockSize, poffset, 0 });
			}
			else
			{
				PackUInts(data, 0, new uint[] { perase_size, pnum_blocks, pblockSize, poffset });
			}

			await CheckCommandAsync("enter flash download mode",
			Device.ESP_FLASH_BEGIN,
			data,
			0,
			cancellationToken,
			timeout2);
			return num_blocks;
		}
		async Task FlashFinishAsync(CancellationToken cancellationToken, bool reboot = false, int timeout = -1)
		{
			// not sure this even should be used
			CheckReady();
			// Leave compressed flash mode and run/reboot

			if (!reboot && !IsStub)
			{
				// skip sending flash_finish to ROM loader, as this
				// exits the bootloader. Stub doesn't do this.
				return;
			}
			uint not_reboot = !reboot ? (uint)1 : 0;
			if (!BitConverter.IsLittleEndian)
			{
				SwapBytes(not_reboot);
			}
			var data = BitConverter.GetBytes(not_reboot);
			await CheckCommandAsync("leave compressed flash mode", Device.ESP_FLASH_END, data, 0, cancellationToken, timeout);
			_inBootloader = false;
		}
		async Task FlashBlockAsync(CancellationToken cancellationToken, byte[] data, uint seq, int attempts = 3, int timeout = -1)
		{
			// """Write block to flash, retry if fail"""
			CheckReady();
			if (attempts < 1) attempts = 1;
			// Write block to flash, send compressed, retry if fail
			Exception lastErr = null;
			while (attempts-- > 0)
			{
				try
				{
					var pck = new byte[16 + data.Length];
					PackUInts(pck, 0, new uint[] { (uint)data.Length, seq, 0, 0 });
					Array.Copy(data, 0, pck, 16, data.Length);
					await CheckCommandAsync(
					$"write compressed data to flash after seq {seq}",
					Device.ESP_FLASH_DATA, pck, Checksum(data, 0, data.Length), cancellationToken, timeout);
					return;

				}
				catch (Exception e)
				{
					lastErr = e;
				}
			}
			if (lastErr == null)
			{
				lastErr = new IOException("The retry count was exceeded");
			}
			throw lastErr;
		}

		async Task<uint> FlashDeflBeginAsync(CancellationToken cancellationToken, uint size, uint compsize, uint offset, uint blockSize, int timeout=-1) {
            CheckReady();
            // Start downloading compressed data to Flash (performs an erase)
            // Returns number of blocks (size self.FLASH_WRITE_SIZE) to write.
            if(blockSize==0)
            {
                blockSize = Device.FLASH_WRITE_SIZE;
            }
            var num_blocks = (compsize + blockSize - 1) / blockSize;
            var erase_blocks = (size + blockSize- 1) / blockSize;
            uint write_size;
            if (IsStub) {
                write_size = (
                    size  // stub expects number of bytes here, manages erasing internally
                );
            }

            write_size = erase_blocks * blockSize;
			int tm = -1;
			if(timeout>-1)
            {
                tm = (int)(1000*( ERASE_REGION_TIMEOUT_PER_MB * ((float)write_size / 1e6)));
                if(tm<timeout)
                {
                    tm = timeout;
                }
			}
            // ROM expects rounded up to erase block size
            timeout = (timeout>-1)?ERASE_REGION_TIMEOUT_PER_MB:timeout;
            // ROM performs the erase up front

            var data = new byte[(!IsStub && Device.SUPPORTS_ENCRYPTED_FLASH) ? 20 : 16];
            var flash_write_size = blockSize;
            if (!BitConverter.IsLittleEndian) {
                flash_write_size = SwapBytes(flash_write_size);
                write_size = SwapBytes(write_size);
                num_blocks = SwapBytes(num_blocks);
                offset = SwapBytes(offset);
            }
            PackUInts(data, 0, new uint[] { write_size, num_blocks, flash_write_size, offset });
            await CheckCommandAsync("enter compressed flash mode",
            Device.ESP_FLASH_DEFL_BEGIN,
            data,
            0,
            cancellationToken,
            tm);
            return num_blocks;
        }
		async Task FlashDeflFinishAsync(CancellationToken cancellationToken, bool reboot = false, int timeout = -1)
        {
            // not sure this even should be used
			CheckReady();
			// Leave compressed flash mode and run/reboot

			if (!reboot && !IsStub) {
                // skip sending flash_finish to ROM loader, as this
                // exits the bootloader. Stub doesn't do this.
                return;
			}
            uint not_reboot = !reboot ? (uint)1 : 0;
            if(!BitConverter.IsLittleEndian)
            {
                SwapBytes(not_reboot);
            }
            var data = BitConverter.GetBytes(not_reboot);
            await CheckCommandAsync("leave compressed flash mode", Device.ESP_FLASH_DEFL_END, data, 0, cancellationToken, timeout);
            _inBootloader = false;
		}
        async Task FlashDeflBlockAsync(CancellationToken cancellationToken, byte[] data, uint seq, int attempts = 3, int timeout = -1) {
			CheckReady();
			if (attempts < 1) attempts = 1;
            // Write block to flash, send compressed, retry if fail
            Exception lastErr = null;
            while(attempts-->0)
            {
                try
                {
                    var pck = new byte[16+data.Length];
                    PackUInts(pck, 0, new uint[] { (uint)data.Length, seq, 0, 0 });
					Array.Copy(data,0,pck,16,data.Length);
                    await CheckCommandAsync(
                    $"write compressed data to flash after seq {seq}",
                    Device.ESP_FLASH_DEFL_DATA, pck, Checksum(data, 0, data.Length),cancellationToken, timeout);
                    //System.Diagnostics.Debug.WriteLine("Wrote packet");
                    return;

                }
                catch (Exception e)
                {
                    lastErr = e;
                }
            }
            if(lastErr==null)
            {
                lastErr = new IOException("The retry count was exceeded");
            }
            throw lastErr;
        }
		/// <summary>
		/// Flashes a binary image to a device
		/// </summary>
		/// <param name="uncompressedInput">An uncompressed raw binary image to flash</param>
		/// <param name="compress">True to compress the image and save bandwidth, otherwise false</param>
		/// <param name="blockSize">The size of each block to write</param>
		/// <param name="offset">The offset in the flash region where the write is to begin</param>
		/// <param name="writeAttempts">The number of attempts to write each block before failing</param>
		/// <param name="finalize">True to finalize the flash and exit the bootloader (not necessary)</param>
		/// <param name="timeout">The timeout for each suboperation</param>
		/// <param name="progress">A <see cref="IProgress{Int32}"/> implementation to report progress</param>
		public void Flash(Stream uncompressedInput, bool compress=true,uint blockSize = 0, uint offset =0x10000, int writeAttempts = 3, bool finalize = false, int timeout = -1, IProgress<int> progress = null)
        {
            FlashAsync(CancellationToken.None,uncompressedInput,compress,blockSize, offset, writeAttempts, finalize, timeout,progress).Wait();
        }
		/// <summary>
		/// Asynchronously flashes a binary image to a device
		/// </summary>
		/// <param name="cancellationToken">The cancellation token to allow for the operation to be canceled</param>
		/// <param name="uncompressedInput">An uncompressed raw binary image to flash</param>
		/// <param name="compress">True to compress the image and save bandwidth, otherwise false</param>
		/// <param name="blockSize">The size of each block to write</param>
		/// <param name="offset">The offset in the flash region where the write is to begin</param>
		/// <param name="writeAttempts">The number of attempts to write each block before failing</param>
		/// <param name="finalize">True to finalize the flash and exit the bootloader (not necessary)</param>
		/// <param name="timeout">The timeout for each suboperation</param>
		/// <param name="progress">A <see cref="IProgress{Int32}"/> implementation to report progress</param>
		public async Task FlashAsync(CancellationToken cancellationToken, Stream uncompressedInput,bool compress=true, uint blockSize=0, uint offset=0x10000, int writeAttempts = 3, bool finalize=false, int timeout = -1,IProgress<int> progress = null)
        {
            CheckReady();
            if(blockSize==0)
            {
                blockSize = Device.FLASH_WRITE_SIZE;
            }
			Stream stm;
			if (compress)
			{
				stm = new MemoryStream(uncompressedInput.Length <= int.MaxValue ? (int)uncompressedInput.Length : int.MaxValue);
				await CompressToZlibStreamAsync(uncompressedInput, stm, cancellationToken);
				stm.Position = 0;
			} else
			{
				stm = uncompressedInput;
			}
            var uclen = (uint)uncompressedInput.Length;
            var cln = (uint)stm.Length;
            progress?.Report(0);
			uint blockCount;
			if (compress)
			{
				blockCount = await FlashDeflBeginAsync(cancellationToken, uclen, cln, offset, blockSize, timeout);
			} else
			{
				blockCount = await FlashBeginAsync(cancellationToken, uclen, cln, offset, blockSize, timeout);
			}
            var block = new byte[blockSize];
            for(int i = 0;i<blockCount;++i)
            {
                
                var bytesRead = await stm.ReadAsync(block, 0, block.Length);
                // pad any unread portion with 0xFF
                for(int j = bytesRead;j<block.Length;++j)
                {
                    block[j] = 0xFF;
                }
				if (compress)
				{
					await FlashDeflBlockAsync(cancellationToken, block, (uint)i, writeAttempts, timeout);
				} else
				{
					await FlashBlockAsync(cancellationToken, block, (uint)i, writeAttempts, timeout);
				}
				progress?.Report((int)((i*100)/blockCount));
			}
            stm.Close();
            stm = null;
            if (IsStub) {
                // Stub only writes each block to flash after 'ack'ing the receive,
                // so do a final dummy operation which will not be 'ack'ed
                // until the last block has actually been written out to flash
                await ReadRegAsync(Device.CHIP_DETECT_MAGIC_REG_ADDR,cancellationToken, timeout);
            }
			if (finalize)
            {
				// this isn't necessary and exits the bootloader preventing further bootloader commands being issued.
				if (compress)
				{
					await FlashDeflFinishAsync(cancellationToken, false, timeout);
				} else
				{
					await FlashFinishAsync(cancellationToken, false, timeout);
				}
            }
			progress?.Report(100);

		}
		async static Task CompressToZlibStreamAsync(Stream inputStream, Stream compressedStream,CancellationToken cancellationToken)
		{
			// Write the zlib header (0x78, 0x9C for default compression)
			compressedStream.WriteByte(0x78);
			compressedStream.WriteByte(0x9C);

			// Use DeflateStream to compress the data (without zlib header/footer)
			using (DeflateStream deflateStream = new DeflateStream(compressedStream, CompressionLevel.Optimal, leaveOpen: true))
			{
				await inputStream.CopyToAsync(deflateStream,81920,cancellationToken);
			}

			// Calculate the zlib footer (Adler-32 checksum) for the compressed data
			byte[] adler32Checksum = Adler32Checksum(inputStream);
			compressedStream.Write(adler32Checksum, 0, adler32Checksum.Length);
		}
		static byte[] Adler32Checksum(Stream stream)
		{
			const uint MOD_ADLER = 65521;
			long position = stream.Position;
			stream.Position = 0; // Reset stream position

			uint a = 1, b = 0;

			int currentByte;
			while ((currentByte = stream.ReadByte()) > -1)
			{
				a = (a + (uint)currentByte) % MOD_ADLER;
				b = (b + a) % MOD_ADLER;
			}

			stream.Position = position; // Restore stream position

			uint checksum = b << 16 | a;

			byte[] result = new byte[4];
			result[0] = (byte)(checksum >> 24 & 0xFF);
			result[1] = (byte)(checksum >> 16 & 0xFF);
			result[2] = (byte)(checksum >> 8 & 0xFF);
			result[3] = (byte)(checksum & 0xFF);

			return result;
		}
	}
}
