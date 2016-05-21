using System;
using NUnit.Framework;
using TinyIpc.IO;
using System.Text;

namespace TinyIpc.Tests
{
	[TestFixture]
	public class TinyMemoryMappedFileTests
	{
		[TestCase(null)]
		[TestCase("")]
		[TestCase(" ")]
		public void Calling_constructor_with_no_name_should_throw(string name)
		{
			Assert.That(() => new TinyMemoryMappedFile(name), Throws.ArgumentException);
		}

		[TestCase(0)]
		[TestCase(-1)]
		public void Calling_constructor_with_invalid_max_file_size_should_throw(long maxFileSize)
		{
			Assert.That(() => new TinyMemoryMappedFile("Test", maxFileSize), Throws.ArgumentException);
		}

		[TestCase("")]
		[TestCase("test")]
		[TestCase("lorem ipsum dolor sit amet")]
		public void Write_then_read_returns_what_was_written(string message)
		{
			using (var file = new TinyMemoryMappedFile("Test"))
			{
				var data = Encoding.UTF8.GetBytes(message);

				file.Write(data);

				Assert.That(file.Read(), Is.EqualTo(data));
			}
		}

		[Test]
		public void Write_with_more_data_then_size_limit_throws()
		{
			using (var file = new TinyMemoryMappedFile("Test", 4))
			{
				Assert.That(() => file.Write(new byte[] { 1, 2, 3, 4, 5 }), Throws.InstanceOf<ArgumentOutOfRangeException>());
			}
		}

		[TestCase("", 0)]
		[TestCase("test", 4)]
		[TestCase("lorem ipsum dolor sit amet", 26)]
		public void GetFileSize_returns_expected_size(string message, int expectedSize)
		{
			using (var file = new TinyMemoryMappedFile("Test"))
			{
				var data = Encoding.UTF8.GetBytes(message);

				file.Write(data);

				Assert.That(file.GetFileSize(), Is.EqualTo(expectedSize));
			}
		}

		[Test]
		public void Dispose_destroys_file()
		{
			using (var file = new TinyMemoryMappedFile("Test"))
			{
				file.Write(new byte[] { 1, 2, 3, 4, 5 });
			}

			using (var file = new TinyMemoryMappedFile("Test"))
			{
				Assert.That(file.GetFileSize(), Is.EqualTo(0));
			}
		}

		[Test]
		public void Secondary_instance_keeps_file_alive()
		{
			using (var file2 = new TinyMemoryMappedFile("Test"))
			{
				using (var file1 = new TinyMemoryMappedFile("Test"))
				{
					file1.Write(new byte[] { 1, 2, 3, 4, 5 });
				}

				Assert.That(file2.GetFileSize(), Is.EqualTo(5));
			}
		}
	}
}
