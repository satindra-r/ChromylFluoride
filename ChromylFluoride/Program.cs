using System.Runtime.InteropServices;
using ComputeSharp;
using NAudio.Wave;

namespace ChromylFluoride;

public static partial class Program {
	private const int SrcCopy = 0x00CC0020;

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

	private static readonly int[] Timings = [10, 15, 5, 15, 10, 15, 10];
	private static readonly int[] CumulativeTimings = new int[Timings.Length];

	private static int _screenWidth, _screenHeight, _totalPixels;
	private static bool _running = true;

	private static void GetScreenSize() {
		var hdc = GetDC(IntPtr.Zero);
		_screenWidth = GetDeviceCaps(hdc, 118);
		_screenHeight = GetDeviceCaps(hdc, 117);
		_totalPixels = _screenWidth * _screenHeight;
		_ = ReleaseDC(IntPtr.Zero, hdc);
	}

	private static void ComputeCumulativeTimings() {
		CumulativeTimings[0] = Timings[0];
		for (var i = 1; i < Timings.Length; i++) {
			CumulativeTimings[i] = CumulativeTimings[i - 1] + Timings[i];
		}
	}

	private static unsafe void ApplyEffect(IntPtr hdcScreen, IntPtr hdcMem, uint* bits, int rand, int timeElapsed) {
		timeElapsed %= 80;
		if (timeElapsed > 70) {
			return;
		}


		BitBlt(hdcMem, 0, 0, _screenWidth, _screenHeight, hdcScreen, 0, 0, SrcCopy);
		using var bufferRead = GraphicsDevice.GetDefault()
			.AllocateReadOnlyBuffer<uint>(new Span<uint>(bits, _totalPixels));
		using var bufferWrite = GraphicsDevice.GetDefault()
			.AllocateReadWriteBuffer<uint>(new Span<uint>(bits, _totalPixels));

		if (timeElapsed < CumulativeTimings[0]) {
			GraphicsDevice.GetDefault().For(bufferRead.Length,
				new LsbCorrupt(bufferRead, bufferWrite, _screenWidth, _screenHeight, rand));
		}
		else if (timeElapsed < CumulativeTimings[1]) {
			GraphicsDevice.GetDefault().For(bufferRead.Length,
				new HueShift(bufferRead, bufferWrite, _screenWidth, _screenHeight, rand));
		}
		else if (timeElapsed < CumulativeTimings[2]) {
			GraphicsDevice.GetDefault().For(bufferRead.Length,
				new Jitter(bufferRead, bufferWrite, _screenWidth, _screenHeight, rand));
		}
		else if (timeElapsed < CumulativeTimings[3]) {
			GraphicsDevice.GetDefault().For(bufferRead.Length,
				new PixelWalk(bufferRead, bufferWrite, _screenWidth, _screenHeight, rand));
		}
		else if (timeElapsed < CumulativeTimings[4]) {
			GraphicsDevice.GetDefault().For(bufferRead.Length,
				new BigMelt(bufferRead, bufferWrite, _screenWidth, _screenHeight, rand));
		}
		else if (timeElapsed < CumulativeTimings[5]) {
			GraphicsDevice.GetDefault().For(bufferRead.Length,
				new SmallMelt(bufferRead, bufferWrite, _screenWidth, _screenHeight, rand));
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
				biCompression = 0
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
		using var waveOut = new WaveOutEvent();
		while (_running) {
			int timeElapsed = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - startTime) %
			                  CumulativeTimings[Timings.Length - 1];
			if (timeElapsed < CumulativeTimings[Timings.Length - 2]) {
				int payload = 0;
				for (int i = 0; i < Timings.Length; i++) {
					if (timeElapsed >= CumulativeTimings[i]) {
						payload++;
					}
				}

				int duration = 200;
				var sineWaveGenerator = new WaveProvider32 {
					Amplitude = 0.25f,
					Duration = duration,
					Payload = payload
				};
				waveOut.Init(sineWaveGenerator);
				waveOut.Play();
				Thread.Sleep(duration);
				waveOut.Stop();
			}

			Thread.Sleep(50);
		}
	}

	private static void CheckExit() {
		while (_running) {
			if ((GetAsyncKeyState(0x1B) & 0x8000) != 0) {
				_running = false;
				break;
			}

			Thread.Sleep(500);
		}
	}

	private static void Main() {
		SetProcessDPIAware();
		GetScreenSize();
		ComputeCumulativeTimings();


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
	public float Amplitude { get; init; }
	public int Duration { get; init; }
	public int Payload { get; init; }

	private double _time;
	private double _frequency;

	public WaveFormat WaveFormat => WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);

	public int Read(float[] buffer, int offset, int count) {
		var randGen = new Random();
		_frequency = 220 * Math.Pow(2, (float)randGen.Next(36) / 12);
		for (var n = 0; n < count; n++) {
			buffer[offset + n] = 0;
			switch (Payload) {
				case 0:
					buffer[offset + n] += (float)((randGen.NextDouble() - 0.5) * Amplitude / 8);
					break;
				case 1:
				case 2:
					for (var i = 0; i < 10; i++) {
						buffer[offset + n] +=
							(float)(Math.Exp(-5.0 * _time * 1000f / Duration) * Math.Pow(i + 1, -2) * Amplitude *
							        Math.Sin((i + 1) * _frequency * (2 * Math.PI * _time)));
					}

					break;
				case 3:
					for (var i = 0; i < 10; i++) {
						buffer[offset + n] +=
							(float)((1 - randGen.NextDouble() * 0.1f) * Math.Exp(-5.0 * _time * 1000f / Duration) *
							        Math.Pow(i + 1, -2) * Amplitude *
							        Math.Sin((i + 1) * _frequency * (2 * Math.PI * _time)));
					}

					break;
				case 4:
				case 5:
					for (var i = 0; i < 10; i++) {
						buffer[offset + n] +=
							(float)(Math.Exp(-5.0 * _time * 1000f / Duration) * Math.Pow(i + 1, -2) * Amplitude *
							        Math.Sin(((i + 1) * _frequency + 5 * Math.Sin(25 * 2 * Math.PI * _time)) *
							                 (2 * Math.PI * _time)));
					}
					break;
			}

			_time += 1.0f / WaveFormat.SampleRate;
		}

		return count;
	}
}