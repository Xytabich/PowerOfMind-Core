namespace PowerOfMind.Systems.Graphics
{
	public interface ITextureSampler
	{
		void Allocate();

		void Release();

		void Bind(int textureNumber);

		void Unbind(int textureNumber);
	}
}