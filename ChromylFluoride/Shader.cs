using ComputeSharp;

namespace ChromylFluoride;

[AutoConstructor]
public readonly partial struct HueShift : IComputeShader {
	private readonly ReadOnlyBuffer<uint> _bufferRead;
	private readonly ReadWriteBuffer<uint> _bufferWrite;
	private readonly int _screenWidth;
	private readonly int _screenHeight;
	private readonly int _seed;

	private static int3 GetRgb(uint pixel) {
		return new int3((int)(pixel & 0x00ff0000) >> 16, (int)(pixel & 0x0000ff00) >> 8, (int)(pixel & 0x000000ff));
	}

	private static uint ToRgb(int r, int g, int b) {
		return 0xff000000 | (uint)r << 16 | (uint)g << 8 | (uint)b;
	}

	public void Execute() {
		var rgb = GetRgb(_bufferRead[ThreadIds.X]);
		var inc = 0;
		var range = Hlsl.Max(Hlsl.Max(rgb.R, rgb.G), rgb.B) - Hlsl.Min(Hlsl.Min(rgb.R, rgb.G), rgb.B);
		if (_seed % 256 < range) {
			inc = 15;
		}

		if (rgb.R > rgb.G && rgb.R >= rgb.B) {
			if (rgb.G >= rgb.B) {
				rgb.G += inc;
				rgb.G = Math.Min(rgb.R, rgb.G);
			}
			else {
				rgb.B -= inc;
				rgb.B = Math.Max(rgb.G, rgb.B);
			}
		}
		else if (rgb.G > rgb.B && rgb.G >= rgb.R) {
			if (rgb.B >= rgb.R) {
				rgb.B += inc;
				rgb.B = Math.Min(rgb.G, rgb.B);
			}
			else {
				rgb.R -= inc;
				rgb.R = Math.Max(rgb.B, rgb.R);
			}
		}
		else if (rgb.B > rgb.R && rgb.B >= rgb.G) {
			if (rgb.R >= rgb.G) {
				rgb.R += inc;
				rgb.R = Math.Min(rgb.B, rgb.R);
			}
			else {
				rgb.G -= inc;
				rgb.G = Math.Max(rgb.R, rgb.G);
			}
		}

		_bufferWrite[ThreadIds.X] = ToRgb(rgb.R, rgb.G, rgb.B);
	}
}

[AutoConstructor]
public readonly partial struct LsbCorrupt : IComputeShader {
	private readonly ReadOnlyBuffer<uint> _bufferRead;
	private readonly ReadWriteBuffer<uint> _bufferWrite;
	private readonly int _screenWidth;
	private readonly int _screenHeight;
	private readonly int _seed;

	private static uint GenRand(uint seed) {
		seed ^= seed << 13;
		seed ^= seed >> 17;
		seed ^= seed << 5;
		return seed;
	}

	public void Execute() {
		var rand = GenRand((uint)(_seed * ThreadIds.X + 1));
		_bufferWrite[ThreadIds.X] = (_bufferRead[ThreadIds.X] & 0xffe0e0e0) | (rand & 0x001f1f1f);
	}
}

[AutoConstructor]
public readonly partial struct BigMelt : IComputeShader {
	private readonly ReadOnlyBuffer<uint> _bufferRead;
	private readonly ReadWriteBuffer<uint> _bufferWrite;
	private readonly int _screenWidth;
	private readonly int _screenHeight;
	private readonly int _seed;

	int Mod(int a, int b) {
		return (a % b + b) % b;
	}

	public void Execute() {
		var x = ThreadIds.X % _screenWidth;
		var y = ThreadIds.X / _screenWidth;
		var yTarget = (int)(256 * Hlsl.Sin(x * 2 * (float)Math.PI / _screenWidth));
		var xTarget = (int)(256 * Hlsl.Sin(y * 2 * (float)Math.PI / _screenHeight));
		var xPos = x;
		var yPos = y;

		if (_seed % 256 < Hlsl.Abs(yTarget)) {
			yPos = Mod(y - Hlsl.Sign(yTarget), _screenHeight);
		}

		if ((_seed / 256) % 256 < Hlsl.Abs(xTarget)) {
			xPos = Mod(x - Hlsl.Sign(xTarget), _screenWidth);
		}

		_bufferWrite[ThreadIds.X] =
			_bufferRead[yPos * _screenWidth + xPos];
	}
}

[AutoConstructor]
public readonly partial struct SmallMelt : IComputeShader {
	private readonly ReadOnlyBuffer<uint> _bufferRead;
	private readonly ReadWriteBuffer<uint> _bufferWrite;
	private readonly int _screenWidth;
	private readonly int _screenHeight;
	private readonly int _seed;

	int Mod(int a, int b) {
		return (a % b + b) % b;
	}

	public void Execute() {
		var x = ThreadIds.X % _screenWidth;
		var y = ThreadIds.X / _screenWidth;
		var yTarget = (int)(256 * Hlsl.Sin(x * 16 * (float)Math.PI / _screenWidth));
		var xTarget = (int)(256 * Hlsl.Sin(y * 16 * (float)Math.PI / _screenHeight));
		var xPos = x;
		var yPos = y;

		if (_seed % 256 < Hlsl.Abs(yTarget)) {
			yPos = Mod(y - Hlsl.Sign(yTarget), _screenHeight);
		}

		if ((_seed / 256) % 256 < Hlsl.Abs(xTarget)) {
			xPos = Mod(x - Hlsl.Sign(xTarget), _screenWidth);
		}

		_bufferWrite[ThreadIds.X] =
			_bufferRead[yPos * _screenWidth + xPos];
	}
}

[AutoConstructor]
public readonly partial struct PixelWalk : IComputeShader {
	private readonly ReadOnlyBuffer<uint> _bufferRead;
	private readonly ReadWriteBuffer<uint> _bufferWrite;
	private readonly int _screenWidth;
	private readonly int _screenHeight;
	private readonly int _seed;

	private static uint GenRand(uint seed) {
		seed ^= seed << 13;
		seed ^= seed >> 17;
		seed ^= seed << 5;
		return seed;
	}

	public void Execute() {
		var x = ThreadIds.X % _screenWidth;
		var y = ThreadIds.X / _screenWidth;

		var rand = Hlsl.Abs((int)GenRand((uint)(_seed * ThreadIds.X + 1)));
		var xShift = (rand & 1) == 1;
		var yShift = (rand & 2) == 2;
		var xPos = x;
		var yPos = y;

		rand >>= 2;
		if (xShift) {
			var dx = rand % 3 - 1;
			xPos = x + dx;
		}

		if (yShift) {
			var dy = rand / 3 % 3 - 1;
			yPos = y + dy;
		}

		if (xPos < 0) {
			xPos += _screenWidth;
		}

		if (xPos > _screenWidth - 1) {
			xPos -= _screenWidth;
		}

		if (yPos < 0) {
			yPos += _screenHeight;
		}

		if (yPos > _screenHeight - 1) {
			yPos -= _screenHeight;
		}

		_bufferWrite[ThreadIds.X] = _bufferRead[yPos * _screenWidth + xPos];
	}
}

[AutoConstructor]
public readonly partial struct Gaussian : IComputeShader {
	private readonly ReadOnlyBuffer<uint> _bufferRead;
	private readonly ReadWriteBuffer<uint> _bufferWrite;
	private readonly int _screenWidth;
	private readonly int _screenHeight;
	private readonly int _seed;

	private static int3 GetRgb(uint pixel) {
		return new int3((int)(pixel & 0x00ff0000) >> 16, (int)(pixel & 0x0000ff00) >> 8, (int)(pixel & 0x000000ff));
	}

	private static uint ToRgb(int r, int g, int b) {
		return 0xff000000 | (uint)r << 16 | (uint)g << 8 | (uint)b;
	}

	public void Execute() {
		var kernel = new float3x3(
			0.0625f, 0.125f, 0.0625f,
			0.125f, 0.25f, 0.125f,
			0.0625f, 0.125f, 0.0625f
		);
		var x = ThreadIds.X % _screenWidth;
		var y = ThreadIds.X / _screenWidth;
		var xPrev = Hlsl.Max(x - 1, 0);
		var yPrev = Hlsl.Max(y - 1, 0);
		var xNext = Hlsl.Min(x + 1, _screenWidth - 1);
		var yNext = Hlsl.Min(y + 1, _screenHeight - 1);
		var xPos = new int3(xPrev, x, xNext);
		var yPos = new int3(yPrev, y, yNext);

		float sumR = 0;
		float sumG = 0;
		float sumB = 0;
		for (var dx = 0; dx < 3; dx++) {
			for (var dy = 0; dy < 3; dy++) {
				var rgb = GetRgb(_bufferRead[yPos[dy] * _screenWidth + xPos[dx]]);
				sumR += kernel[dy][dx] * rgb.R;
				sumG += kernel[dy][dx] * rgb.G;
				sumB += kernel[dy][dx] * rgb.B;
			}
		}

		var sumClampedR = (int)Hlsl.Round(Hlsl.Clamp(sumR, 0.0f, 255.0f));
		var sumClampedG = (int)Hlsl.Round(Hlsl.Clamp(sumG, 0.0f, 255.0f));
		var sumClampedB = (int)Hlsl.Round(Hlsl.Clamp(sumB, 0.0f, 255.0f));

		_bufferWrite[ThreadIds.X] = ToRgb(sumClampedR, sumClampedG, sumClampedB);
	}
}