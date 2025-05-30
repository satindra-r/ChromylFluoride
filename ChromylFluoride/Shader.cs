﻿using ComputeSharp;

namespace ChromylFluoride;

[AutoConstructor]
public readonly partial struct HueShift : IComputeShader {
	private readonly ReadOnlyBuffer<uint> _bufferRead;
	private readonly ReadWriteBuffer<uint> _bufferWrite;
	private readonly int _screenWidth;
	private readonly int _screenHeight;
	private readonly int _seed;

	private static int3 SplitColours(uint pixel) {
		return new int3((int)(pixel & 0x00ff0000) >> 16, (int)(pixel & 0x0000ff00) >> 8, (int)(pixel & 0x000000ff));
	}

	private static uint JoinColours(int r, int g, int b) {
		return 0xff000000 | (uint)r << 16 | (uint)g << 8 | (uint)b;
	}

	public void Execute() {
		var rgb = SplitColours(_bufferRead[ThreadIds.X]);
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

		_bufferWrite[ThreadIds.X] = JoinColours(rgb.R, rgb.G, rgb.B);
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
		if (a >= b) {
			return a - b;
		}

		if (a < 0) {
			return a + b;
		}

		return a;
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
		if (a >= b) {
			return a - b;
		}

		if (a < 0) {
			return a + b;
		}

		return a;
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

	int Mod(int a, int b) {
		if (a >= b) {
			return a - b;
		}

		if (a < 0) {
			return a + b;
		}

		return a;
	}

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
			var dx = (rand % 3) - 1;
			xPos = Mod(x + dx, _screenWidth);
		}

		if (yShift) {
			var dy = ((rand / 3) % 3) - 1;
			yPos = Mod(y + dy, _screenHeight);
		}

		_bufferWrite[ThreadIds.X] = _bufferRead[yPos * _screenWidth + xPos];
	}
}

[AutoConstructor]
public readonly partial struct Jitter : IComputeShader {
	private readonly ReadOnlyBuffer<uint> _bufferRead;
	private readonly ReadWriteBuffer<uint> _bufferWrite;
	private readonly int _screenWidth;
	private readonly int _screenHeight;
	private readonly int _seed;

	private static int3 SplitColours(uint pixel) {
		return new int3((int)(pixel & 0x00ff0000) >> 16, (int)(pixel & 0x0000ff00) >> 8, (int)(pixel & 0x000000ff));
	}

	private static uint JoinColours(int r, int g, int b) {
		return 0xff000000 | (uint)r << 16 | (uint)g << 8 | (uint)b;
	}

	int Mod(int a, int b) {
		if (a >= b) {
			return a - b;
		}

		if (a < 0) {
			return a + b;
		}

		return a;
	}

	public void Execute() {
		var x = ThreadIds.X % _screenWidth;
		var y = ThreadIds.X / _screenWidth;
		var xPos = new int3(Mod(x + ((_seed % 32) - 16), _screenWidth),
			Mod(x + (((_seed >> 5) % 32) - 16), _screenWidth), Mod(x + (((_seed >> 10) % 32) - 16), _screenWidth));
		var yPos = new int3(Mod(y + (((_seed >> 15) % 32) - 16), _screenHeight),
			Mod(y + (((_seed >> 20) % 32) - 16), _screenHeight), Mod(y + (((_seed >> 25) % 32) - 16), _screenHeight));
		_bufferWrite[ThreadIds.X] = JoinColours(SplitColours(_bufferRead[yPos[0] * _screenWidth + xPos[0]]).R,SplitColours(_bufferRead[yPos[1] * _screenWidth + xPos[1]]).G,SplitColours(_bufferRead[yPos[2] * _screenWidth + xPos[2]]).B);
	}
}