using System;
using System.Text;
using Shouldly;
using Xunit;

namespace TinyIpc.IO
{
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
			using var file = new TinyMemoryMappedFile("Test");

			var data = Encoding.UTF8.GetBytes(message);

			file.Write(data);

			file.Read().ShouldBe(data);
		}

		[Fact]
		public void Write_with_more_data_than_size_limit_throws()
		{
			using var file = new TinyMemoryMappedFile("Test", 4);

			Should.Throw<ArgumentOutOfRangeException>(() => file.Write(new byte[] { 1, 2, 3, 4, 5 }));
		}

		[Theory]
		[InlineData("")]
		[InlineData("test")]
		[InlineData("lorem ipsum dolor sit amet")]
		public void GetFileSize_returns_expected_size(string message)
		{
			using var file = new TinyMemoryMappedFile("Test");

			var data = Encoding.UTF8.GetBytes(message);

			file.Write(data);

			file.GetFileSize().ShouldBe(message.Length);
		}

		[Fact]
		public void Dispose_destroys_file()
		{
			using (var file = new TinyMemoryMappedFile("Test"))
			{
				file.Write(new byte[] { 1, 2, 3, 4, 5 });
			}

			using (var file = new TinyMemoryMappedFile("Test"))
			{
				file.GetFileSize().ShouldBe(0);
			}
		}

		[Fact]
		public void Secondary_instance_keeps_file_alive()
		{
			using var file2 = new TinyMemoryMappedFile("Test");

			using (var file1 = new TinyMemoryMappedFile("Test"))
			{
				file1.Write(new byte[] { 1, 2, 3, 4, 5 });
			}

			file2.GetFileSize().ShouldBe(5);
		}
	}
}
