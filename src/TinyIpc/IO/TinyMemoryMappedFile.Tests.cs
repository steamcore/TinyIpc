using System.Text;
using Shouldly;
using Xunit;

namespace TinyIpc.IO;

public class TinyMemoryMappedFileTests
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(" ")]
	public void Calling_constructor_with_no_name_should_throw(string name)
	{
		Should.Throw<ArgumentException>(() => new TinyMemoryMappedFile(name));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Calling_constructor_with_invalid_max_file_size_should_throw(long maxFileSize)
	{
		Should.Throw<ArgumentException>(() => new TinyMemoryMappedFile("Test", maxFileSize));
	}

	[Theory]
	[InlineData("")]
	[InlineData("test")]
	[InlineData("lorem ipsum dolor sit amet")]
	public void Write_then_read_returns_what_was_written(string message)
	{
		using var file = new TinyMemoryMappedFile(name: Guid.NewGuid().ToString());

		var data = Encoding.UTF8.GetBytes(message);
		using var dataStream = new MemoryStream(data);

		file.Write(dataStream, TestContext.Current.CancellationToken);

		file.Read(stream => stream.ToArray(), TestContext.Current.CancellationToken).ShouldBe(data);
	}

	[Fact]
	public void Write_with_more_data_than_size_limit_throws()
	{
		using var file = new TinyMemoryMappedFile(name: Guid.NewGuid().ToString(), maxFileSize: 4);

		using var dataStream = new MemoryStream([1, 2, 3, 4, 5]);

		Should.Throw<ArgumentOutOfRangeException>(() => file.Write(dataStream, TestContext.Current.CancellationToken));
	}

	[Theory]
	[InlineData("")]
	[InlineData("test")]
	[InlineData("lorem ipsum dolor sit amet")]
	public void GetFileSize_returns_expected_size(string message)
	{
		using var file = new TinyMemoryMappedFile(name: Guid.NewGuid().ToString());

		var data = Encoding.UTF8.GetBytes(message);
		using var dataStream = new MemoryStream(data);

		file.Write(dataStream, TestContext.Current.CancellationToken);

		file.GetFileSize(TestContext.Current.CancellationToken).ShouldBe(message.Length);
	}

	[Fact]
	public void Dispose_destroys_file()
	{
		var name = Guid.NewGuid().ToString();

		using (var file = new TinyMemoryMappedFile(name))
		{
			using var dataStream = new MemoryStream([1, 2, 3, 4, 5]);

			file.Write(dataStream, TestContext.Current.CancellationToken);
		}

		using (var file = new TinyMemoryMappedFile(name))
		{
			file.GetFileSize(TestContext.Current.CancellationToken).ShouldBe(0);
		}
	}

	[Fact]
	public void Secondary_instance_keeps_file_alive()
	{
		var name = Guid.NewGuid().ToString();

		using var file2 = new TinyMemoryMappedFile(name);

		using (var file1 = new TinyMemoryMappedFile(name))
		{
			using var dataStream = new MemoryStream([1, 2, 3, 4, 5]);

			file1.Write(dataStream, TestContext.Current.CancellationToken);
		}

		file2.GetFileSize(TestContext.Current.CancellationToken).ShouldBe(5);
	}
}
