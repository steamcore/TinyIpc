using System.Text;
using Shouldly;

namespace TinyIpc.IO;

public class TinyMemoryMappedFileTests
{
	[Test]
	[Arguments(null)]
	[Arguments("")]
	[Arguments(" ")]
	public void Calling_constructor_with_no_name_should_throw(string name)
	{
		Should.Throw<ArgumentException>(() => new TinyMemoryMappedFile(name));
	}

	[Test]
	[Arguments(0)]
	[Arguments(-1)]
	public void Calling_constructor_with_invalid_max_file_size_should_throw(long maxFileSize)
	{
		Should.Throw<ArgumentException>(() => new TinyMemoryMappedFile("Test", maxFileSize));
	}

	[Test]
	[Arguments("")]
	[Arguments("test")]
	[Arguments("lorem ipsum dolor sit amet")]
	public void Write_then_read_returns_what_was_written(string message, CancellationToken cancellationToken)
	{
		using var file = new TinyMemoryMappedFile(name: Guid.NewGuid().ToString());

		var data = Encoding.UTF8.GetBytes(message);
		using var dataStream = new MemoryStream(data);

		file.Write(dataStream, cancellationToken);

		file.Read(stream => stream.ToArray(), cancellationToken).ShouldBe(data);
	}

	[Test]
	public void Write_with_more_data_than_size_limit_throws(CancellationToken cancellationToken)
	{
		using var file = new TinyMemoryMappedFile(name: Guid.NewGuid().ToString(), maxFileSize: 4);

		using var dataStream = new MemoryStream([1, 2, 3, 4, 5]);

		Should.Throw<ArgumentOutOfRangeException>(() => file.Write(dataStream, cancellationToken));
	}

	[Test]
	[Arguments("")]
	[Arguments("test")]
	[Arguments("lorem ipsum dolor sit amet")]
	public void GetFileSize_returns_expected_size(string message, CancellationToken cancellationToken)
	{
		using var file = new TinyMemoryMappedFile(name: Guid.NewGuid().ToString());

		var data = Encoding.UTF8.GetBytes(message);
		using var dataStream = new MemoryStream(data);

		file.Write(dataStream, cancellationToken);

		file.GetFileSize(cancellationToken).ShouldBe(message.Length);
	}

	[Test]
	public void Dispose_destroys_file(CancellationToken cancellationToken)
	{
		var name = Guid.NewGuid().ToString();

		using (var file = new TinyMemoryMappedFile(name))
		{
			using var dataStream = new MemoryStream([1, 2, 3, 4, 5]);

			file.Write(dataStream, cancellationToken);
		}

		using (var file = new TinyMemoryMappedFile(name))
		{
			file.GetFileSize(cancellationToken).ShouldBe(0);
		}
	}

	[Test]
	public void Secondary_instance_keeps_file_alive(CancellationToken cancellationToken)
	{
		var name = Guid.NewGuid().ToString();

		using var file2 = new TinyMemoryMappedFile(name);

		using (var file1 = new TinyMemoryMappedFile(name))
		{
			using var dataStream = new MemoryStream([1, 2, 3, 4, 5]);

			file1.Write(dataStream, cancellationToken);
		}

		file2.GetFileSize(cancellationToken).ShouldBe(5);
	}
}
