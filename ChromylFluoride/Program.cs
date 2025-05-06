using System.Runtime.InteropServices;
using ComputeSharp;
using NAudio.Wave;

namespace ChromylFluoride;

public static partial class Program {
	private const int SrcCopy = 0x00CC0020;
	private const int BiRgb = 0;

	#region Structs

	[StructLayout(LayoutKind.Sequential)]
	private struct BitmapInfoHeader {
		public uint biSize;
		public int biWidth;
		public int biHeight;
		public short biPlanes;
		public short biBitCount;
		public uint biCompression;
		public uint biSizeImage;
		public int biXPelsPerMeter;
		public int biYPelsPerMeter;
		public uint biClrUsed;
		public uint biClrImportant;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BitMapInfo {
		public BitmapInfoHeader bmiHeader;

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)]
		public uint[] bmiColors;
	}

	#endregion

	#region Imports

	[LibraryImport("user32.dll")]
	private static partial IntPtr GetDC(IntPtr hWnd);

	[LibraryImport("user32.dll")]
	private static partial int ReleaseDC(IntPtr hWnd, IntPtr hDc);

	[LibraryImport("gdi32.dll")]
	private static partial IntPtr CreateCompatibleDC(IntPtr hdc);

	[DllImport("gdi32.dll")]
	private static extern unsafe IntPtr CreateDIBSection(IntPtr hdc, ref BitMapInfo bmi, uint usage, out uint* bits,
		IntPtr hSection,
		uint offset);

	[LibraryImport("gdi32.dll")]
	private static partial void SelectObject(IntPtr hdc, IntPtr obj);

	[DllImport("gdi32.dll")]
	private static extern bool BitBlt(IntPtr destDc, int x, int y, int w, int h, IntPtr srcDc, int sx, int sy, int rop);

	[DllImport("gdi32.dll")]
	private static extern bool DeleteObject(IntPtr obj);

	[DllImport("gdi32.dll")]
	private static extern bool DeleteDC(IntPtr hdc);

	[LibraryImport("user32.dll")]
	private static partial short GetAsyncKeyState(int vKey);

	[LibraryImport("gdi32.dll")]
	private static partial int GetDeviceCaps(IntPtr hdc, int index);

	[DllImport("user32.dll")]
	private static extern bool SetProcessDPIAware();

	#endregion

	private static int _screenWidth, _screenHeight, _totalPixels;
	private static bool _running = true;

	private static void GetScreenSize() {
		var hdc = GetDC(IntPtr.Zero);
		_screenWidth = GetDeviceCaps(hdc, 118);
		_screenHeight = GetDeviceCaps(hdc, 117);
		_totalPixels = _screenWidth * _screenHeight;
		_ = ReleaseDC(IntPtr.Zero, hdc);
	}


	private static unsafe void ApplyEffect(IntPtr hdcScreen, IntPtr hdcMem, uint* bits, int rand, int timeElapsed) {
		timeElapsed %= 80;
		timeElapsed += 35;
		if (timeElapsed > 70) {
			return;
		}


		BitBlt(hdcMem, 0, 0, _screenWidth, _screenHeight, hdcScreen, 0, 0, SrcCopy);
		using var bufferRead = GraphicsDevice.GetDefault()
			.AllocateReadOnlyBuffer<uint>(new Span<uint>(bits, _totalPixels));
		using var bufferWrite = GraphicsDevice.GetDefault()
			.AllocateReadWriteBuffer<uint>(new Span<uint>(bits, _totalPixels));

		if (timeElapsed < 5) {
			GraphicsDevice.GetDefault().For(bufferRead.Length,
				new LsbCorrupt(bufferRead, bufferWrite, _screenWidth, _screenHeight, rand));
		}
		else if (timeElapsed < 25) {
			GraphicsDevice.GetDefault().For(bufferRead.Length,
				new HueShift(bufferRead, bufferWrite, _screenWidth, _screenHeight, rand));
		}
		else if (timeElapsed < 40) {
			GraphicsDevice.GetDefault().For(bufferRead.Length,
				new PixelWalk(bufferRead, bufferWrite, _screenWidth, _screenHeight, rand));
		}
		else if (timeElapsed < 50) {
			GraphicsDevice.GetDefault().For(bufferRead.Length,
				new BigMelt(bufferRead, bufferWrite, _screenWidth, _screenHeight, rand));
		}
		else if (timeElapsed < 65) {
			GraphicsDevice.GetDefault().For(bufferRead.Length,
				new SmallMelt(bufferRead, bufferWrite, _screenWidth, _screenHeight, rand));
		}
		else if(timeElapsed < 70) {
			GraphicsDevice.GetDefault().For(bufferRead.Length,
				new Gaussian(bufferRead, bufferWrite, _screenWidth, _screenHeight, rand));
		}


		bufferWrite.CopyTo(new Span<uint>(bits, _totalPixels));
		BitBlt(hdcScreen, 0, 0, _screenWidth, _screenHeight, hdcMem, 0, 0, SrcCopy);
	}

	private static unsafe void GenVisuals() {
		var startTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		var hdcScreen = GetDC(IntPtr.Zero);
		var hdcMem = CreateCompatibleDC(hdcScreen);
		var randGen = new Random();
		var bmi = new BitMapInfo {
			bmiHeader = new BitmapInfoHeader {
				biSize = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
				biWidth = _screenWidth,
				biHeight = -_screenHeight,
				biPlanes = 1,
				biBitCount = 32,
				biCompression = BiRgb
			},
			bmiColors = new uint[1024]
		};

		var hBitmap = CreateDIBSection(hdcScreen, ref bmi, 0, out var bits, IntPtr.Zero, 0);
		SelectObject(hdcMem, hBitmap);
		while (_running) {
			ApplyEffect(hdcScreen, hdcMem, bits, randGen.Next(),
				(int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - startTime));
		}

		DeleteObject(hBitmap);
		DeleteDC(hdcMem);
		_ = ReleaseDC(IntPtr.Zero, hdcScreen);
	}

	private static void GenAudio() {
		var startTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		var randGen = new Random();
		using var waveOut = new WaveOutEvent();
		while (_running) {
			if ((DateTimeOffset.UtcNow.ToUnixTimeSeconds() - startTime) % 50 < 40) {
				var sineWaveGenerator = new WaveProvider32 {
					Frequency = 220 * (float)Math.Pow(2, (float)randGen.Next(36) / 12),
					Amplitude = 0.25f,
					TotalSamples = 441 * 20
				};
				waveOut.Init(sineWaveGenerator);
				waveOut.Play();
				Thread.Sleep(200);
				waveOut.Stop();
			}

			Thread.Sleep(500);
		}
	}

	private static void CheckExit() {
		while (_running) {
			if ((GetAsyncKeyState(0x1B) & 0x8000) != 0) // VK_ESCAPE
			{
				_running = false;
				break;
			}

			Thread.Sleep(50);
		}
	}

	private static void Main() {
		SetProcessDPIAware();
		GetScreenSize();

		var visualThread = new Thread(GenVisuals);
		var audioThread = new Thread(GenAudio);
		var exitThread = new Thread(CheckExit);

		visualThread.Start();
		audioThread.Start();
		exitThread.Start();

		visualThread.Join();
		audioThread.Join();
		exitThread.Join();
	}
}

internal class WaveProvider32 : ISampleProvider {
	public float Frequency { get; init; }
	public float Amplitude { get; init; }
	public int TotalSamples { get; init; }
	private float _phase;
	private int _samplesUsed;

	public WaveFormat WaveFormat => WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);

	public int Read(float[] buffer, int offset, int count) {
		for (var n = 0; n < count; n++) {
			buffer[offset + n] = 0;
			for (var i = 0; i < 10; i++) {
				buffer[offset + n] +=
					(float)(Math.Exp(-5.0 * _samplesUsed / TotalSamples) * Math.Pow(i + 1, -2) * Amplitude *
					        Math.Sin((i + 1) * _phase * 2 * Math.PI));
			}

			_samplesUsed++;
			_phase += Frequency / WaveFormat.SampleRate;
			if (_phase > 1) _phase -= 1;
		}

		return count;
	}
}