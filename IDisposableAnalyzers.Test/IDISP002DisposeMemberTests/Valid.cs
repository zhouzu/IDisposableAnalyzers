﻿namespace IDisposableAnalyzers.Test.IDISP002DisposeMemberTests
{
    using Gu.Roslyn.Asserts;
    using Microsoft.CodeAnalysis.Diagnostics;
    using NUnit.Framework;

    public static partial class Valid
    {
        private static readonly DiagnosticAnalyzer Analyzer = new FieldAndPropertyDeclarationAnalyzer();

        private const string DisposableCode = @"
namespace N
{
    using System;

    public class Disposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}";

        [TestCase("stream.Dispose();")]
        [TestCase("stream?.Dispose();")]
        [TestCase("this.stream.Dispose();")]
        [TestCase("this.stream?.Dispose();")]
        [TestCase("Stream.Dispose();")]
        [TestCase("Stream?.Dispose();")]
        [TestCase("this.Stream.Dispose();")]
        [TestCase("this.Stream?.Dispose();")]
        [TestCase("Calculated.Dispose();")]
        [TestCase("Calculated?.Dispose();")]
        [TestCase("this.Calculated.Dispose();")]
        [TestCase("this.Calculated?.Dispose();")]
        public static void DisposingField(string disposeCall)
        {
            var code = @"
namespace N
{
    using System;
    using System.IO;

    public sealed class C : IDisposable
    {
        private Stream stream = File.OpenRead(string.Empty);

        public Stream Calculated => this.stream;

        public Stream Stream
        {
            get { return this.stream; }
            private set { this.stream = value; }
        }

        public void Dispose()
        {
            this.stream.Dispose();
        }
    }
}".AssertReplace("this.stream.Dispose();", disposeCall);
            RoslynAssert.Valid(Analyzer, code);
        }

        [Test]
        public static void DisposingFieldInVirtualDispose()
        {
            var code = @"
namespace N
{
    using System;
    using System.IO;

    public class C : IDisposable
    {
        private readonly Stream stream = File.OpenRead(string.Empty);
        private bool disposed;

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.stream.Dispose();
            }
        }

        protected void ThrowIfDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }
        }
    }
}";
            RoslynAssert.Valid(Analyzer, code);
        }

        [Test]
        public static void DisposingFieldInVirtualDispose2()
        {
            var disposableCode = @"
namespace N
{
    using System;

    public class Disposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}";
            var code = @"
namespace N
{
    using System;

    public class C : IDisposable
    {
        private readonly IDisposable _disposable = new Disposable();
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _disposable.Dispose();
            }
        }

        protected void VerifyDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }
    }
}";
            RoslynAssert.Valid(Analyzer, disposableCode, code);
        }

        [Test]
        public static void DisposingFieldInExpressionBodyDispose()
        {
            var disposableCode = @"
namespace N
{
    using System;
    class Disposable : IDisposable {
        public void Dispose() { }
    }
}";

            var code = @"
namespace N
{
    using System;
    class Goof : IDisposable {
        IDisposable _disposable;
        public void Create()  => _disposable = new Disposable();
        public void Dispose() => _disposable.Dispose();
    }
}";
            RoslynAssert.Valid(Analyzer, disposableCode, code);
        }

        [Test]
        public static void DisposingFieldAsCast()
        {
            var code = @"
namespace N
{
    using System;
    using System.IO;

    public sealed class C : IDisposable
    {
        private readonly object stream =  File.OpenRead(string.Empty);

        public void Dispose()
        {
            var disposable = this.stream as IDisposable;
            disposable?.Dispose();
        }
    }
}";
            RoslynAssert.Valid(Analyzer, code);
        }

        [Test]
        public static void DisposingFieldInlineAsCast()
        {
            var code = @"
namespace N
{
    using System;
    using System.IO;

    public sealed class C : IDisposable
    {
        private readonly object stream =  File.OpenRead(string.Empty);

        public void Dispose()
        {
            (this.stream as IDisposable)?.Dispose();
        }
    }
}";
            RoslynAssert.Valid(Analyzer, code);
        }

        [Test]
        public static void DisposingFieldExplicitCast()
        {
            var code = @"
namespace N
{
    using System;
    using System.IO;

    public sealed class C : IDisposable
    {
        private readonly object stream =  File.OpenRead(string.Empty);

        public void Dispose()
        {
            var disposable = (IDisposable)this.stream;
            disposable.Dispose();
        }
    }
}";
            RoslynAssert.Valid(Analyzer, code);
        }

        [Test]
        public static void DisposingFieldInlineExplicitCast()
        {
            var code = @"
namespace N
{
    using System;
    using System.IO;

    public sealed class C : IDisposable
    {
        private readonly object stream =  File.OpenRead(string.Empty);

        public void Dispose()
        {
            ((IDisposable)this.stream).Dispose();
        }
    }
}";
            RoslynAssert.Valid(Analyzer, code);
        }

        [Test]
        public static void DisposingPropertyWhenInitializedInProperty()
        {
            var code = @"
namespace N
{
    using System;
    using System.IO;

    public sealed class C : IDisposable
    {
        public C()
        {
            this.Stream = File.OpenRead(string.Empty);
        }

        public Stream Stream { get; private set; }
        
        public void Dispose()
        {
            this.Stream.Dispose();
        }
    }
}";

            RoslynAssert.Valid(Analyzer, code);
        }

        [Test]
        public static void DisposingPropertyWhenInitializedInline()
        {
            var code = @"
namespace N
{
    using System;
    using System.IO;

    public sealed class C : IDisposable
    {
        public Stream Stream { get; private set; } = File.OpenRead(string.Empty);
        
        public void Dispose()
        {
            this.Stream.Dispose();
        }
    }
}";

            RoslynAssert.Valid(Analyzer, code);
        }

        [Test]
        public static void DisposingPropertyInBaseClass()
        {
            var baseClassCode = @"
namespace N
{
    using System;
    using System.IO;

    public abstract class Base : IDisposable
    {
        public abstract Stream Stream { get; }
        
        public void Dispose()
        {
            this.Stream.Dispose();
        }
    }
}";

            var code = @"
namespace N
{
    using System;
    using System.IO;

    public sealed class C : Base
    {
        public override Stream Stream { get; } = File.OpenRead(string.Empty);
    }
}";

            RoslynAssert.Valid(Analyzer, baseClassCode, code);
        }

        [Test]
        public static void DisposingPropertyInVirtualDisposeInBaseClass()
        {
            var baseClassCode = @"
namespace N
{
    using System;
    using System.IO;

    public abstract class Base : IDisposable
    {
        private bool disposed;

        public abstract Stream Stream { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            if (disposing)
            {
                this.Stream?.Dispose();
            }
        }
    }
}";

            var code = @"
namespace N
{
    using System.IO;

    public sealed class C : Base
    {
        public override Stream Stream { get; } = File.OpenRead(string.Empty);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
            }

            base.Dispose(disposing);
        }
    }
}";

            RoslynAssert.Valid(Analyzer, baseClassCode, code);
        }

        [TestCase("disposables.First();")]
        [TestCase("disposables.Single();")]
        public static void IgnoreLinq(string linq)
        {
            var code = @"
namespace N
{
    using System;
    using System.Linq;

    public sealed class C
    {
        private readonly IDisposable _bar;
        
        public C(IDisposable[] disposables)
        {
            _bar = disposables.First();
        }
    }
}".AssertReplace("disposables.First();", linq);
            RoslynAssert.Valid(Analyzer, code);
        }

        [Test]
        public static void IgnoredWhenNotAssigned()
        {
            var code = @"
namespace N
{
    using System;
    using System.IO;

    public sealed class C
    {
        private readonly IDisposable bar;
    }
}";
            RoslynAssert.Valid(Analyzer, code);
        }

        [Test]
        public static void IgnoredWhenBackingField()
        {
            var code = @"
namespace N
{
    using System.IO;

    public sealed class C
    {
        private Stream stream;

        public Stream Stream
        {
            get { return this.stream; }
            set { this.stream = value; }
        }
    }
}";
            RoslynAssert.Valid(Analyzer, code);
        }

        [Test]
        public static void IgnoredWhenBackingFieldWithMethodSettingPropertyToNull()
        {
            var code = @"
namespace N
{
    using System.IO;

    public sealed class C
    {
        private Stream stream;

        public Stream Stream
        {
            get { return this.stream; }
            set { this.stream = value; }
        }

        public void Meh()
        {
            var temp = this.Stream;
            this.Stream = null;
            this.stream = temp;
        }
    }
}";
            RoslynAssert.Valid(Analyzer, code);
        }

        [Test]
        public static void IgnoreFieldThatIsNotDisposable()
        {
            var code = @"
namespace N
{
    public class C
    {
        private readonly object bar = new object();
    }
}";
            RoslynAssert.Valid(Analyzer, code);
        }

        [Test]
        public static void IgnoreFieldThatIsNotDisposableAssignedWithMethod1()
        {
            var code = @"
namespace N
{
    public class C
    {
        private readonly object bar = Meh();

        private static object Meh() => new object();
    }
}";
            RoslynAssert.Valid(Analyzer, code);
        }

        [Test]
        public static void IgnoreFieldThatIsNotDisposableAssignedWIthMethod2()
        {
            var code = @"
namespace N
{
    public class C
    {
        private readonly object bar = string.Copy(string.Empty);
    }
}";
            RoslynAssert.Valid(Analyzer, code);
        }

        [Test]
        public static void IgnoredStaticField()
        {
            var code = @"
namespace N
{
    using System.IO;

    public sealed class C
    {
        private static Stream stream = File.OpenRead(string.Empty);
    }
}";
            RoslynAssert.Valid(Analyzer, code);
        }

        [Test]
        public static void IgnoreTask()
        {
            var code = @"
namespace N
{
    using System.Threading.Tasks;

    public sealed class C
    {
        private readonly Task stream = Task.Delay(0);
    }
}";
            RoslynAssert.Valid(Analyzer, code);
        }

        [Test]
        public static void IgnoreTaskOfInt()
        {
            var code = @"
namespace N
{
    using System.Threading.Tasks;

    public sealed class C
    {
        private readonly Task<int> stream = Task.FromResult(0);
    }
}";
            RoslynAssert.Valid(Analyzer, code);
        }

        [Test]
        public static void FieldOfTypeArrayOfInt()
        {
            var code = @"
namespace N
{
    public sealed class C
    {
        private readonly int[] ints = new[] { 1, 2, 3 };
    }
}";
            RoslynAssert.Valid(Analyzer, code);
        }

        [Test]
        public static void PropertyWithBackingFieldOfTypeArrayOfInt()
        {
            var code = @"
namespace N
{
    public sealed class C
    {
        private int[] ints;

        public int[] Ints
        {
            get
            {
                return this.ints ?? (this.ints = new int[] { });
            }

            set
            {
                this.ints = value;
            }
        }

        public bool HasInts => (this.ints != null) && (this.ints.Length > 0);
    }
}";
            RoslynAssert.Valid(Analyzer, code);
        }

        [Test]
        public static void HandlesRecursion()
        {
            var code = @"
namespace N
{
    using System;

    public class C
    {
        private readonly IDisposable foo = Forever();

        private static IDisposable Forever()
        {
            return Forever();
        }
    }
}";
            RoslynAssert.Valid(Analyzer, code);
        }

        [Test]
        public static void InjectedListOfInt()
        {
            var code = @"
namespace N
{
    using System;
    using System.Collections.Generic;

    public class C
    {
        private readonly List<int> ints;

        public C(List<int> ints)
        {
            this.ints = ints;
        }
    }
}";
            RoslynAssert.Valid(Analyzer, code);
        }

        [Test]
        public static void InjectedListOfT()
        {
            var code = @"
namespace N
{
    using System;
    using System.Collections.Generic;

    public class C<T>
    {
        private readonly List<T> values;

        public C(List<T> values)
        {
            this.values = values;
        }
    }
}";
            RoslynAssert.Valid(Analyzer, code);
        }

        [Test]
        public static void DisposingPropertyInBase()
        {
            var fooCode = @"
namespace N
{
    using System;
    using System.IO;

    public class Base : IDisposable
    {
        public virtual Stream Stream { get; } = File.OpenRead(string.Empty);
        private bool disposed;

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            this.Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.Stream.Dispose();
            }
        }

        protected void ThrowIfDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }
        }
    }
}";

            var barCode = @"
namespace N
{
    using System.IO;

    public class C : Base
    {
        public override Stream Stream { get; }
    }
}";
            RoslynAssert.Valid(Analyzer, fooCode, barCode);
        }

        [Test]
        public static void WhenCallingBaseDispose()
        {
            var fooBaseCode = @"
namespace N
{
    using System;

    public abstract class Base : IDisposable
    {
        private readonly IDisposable disposable = new Disposable();
        private bool disposed;

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            if (disposing)
            {
                this.disposable.Dispose();
            }
        }
    }
}";
            var code = @"
namespace N
{
    public class C : Base
    {
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }
}";

            RoslynAssert.Valid(Analyzer, DisposableCode, fooBaseCode, code);
        }

        [Test]
        public static void DisposingFieldInTearDown()
        {
            var code = @"
namespace N
{
    using NUnit.Framework;

    public class Tests
    {
        private Disposable disposable;

        [SetUp]
        public void SetUp()
        {
            this.disposable = new Disposable();
        }

        [TearDown]
        public void TearDown()
        {
            this.disposable.Dispose();
        }
    }
}";
            RoslynAssert.Valid(Analyzer, DisposableCode, code);
        }

        [Test]
        public static void DisposingFieldInOneTimeTearDown()
        {
            var code = @"
namespace N
{
    using NUnit.Framework;

    public class Tests
    {
        private Disposable disposable;

        [OneTimeSetUp]
        public void SetUp()
        {
            this.disposable = new Disposable();
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            this.disposable.Dispose();
        }
    }
}";
            RoslynAssert.Valid(Analyzer, DisposableCode, code);
        }

        [Test]
        public static void Issue150()
        {
            var code = @"
namespace ValidCode
{
    using System.Collections.Generic;
    using System.IO;

    public class Issue150
    {
        public Issue150(string name)
        {
            this.Name = name;
            if (File.Exists(name))
            {
                this.AllText = File.ReadAllText(name);
                this.AllLines = File.ReadAllLines(name);
            }
        }

        public string Name { get; }

        public bool Exists => File.Exists(this.Name);

        public string AllText { get; }

        public IReadOnlyList<string> AllLines { get; }
    }
}";
            RoslynAssert.Valid(Analyzer, code);
        }

        [TestCase("Tuple.Create(File.OpenRead(file1), File.OpenRead(file2))")]
        [TestCase("new Tuple<FileStream, FileStream>(File.OpenRead(file1), File.OpenRead(file2))")]
        public static void Tuple(string expression)
        {
            var code = @"
namespace N
{
    using System;
    using System.IO;

    public sealed class C : IDisposable
    {
        private readonly Tuple<FileStream, FileStream> tuple;

        public C(string file1, string file2)
        {
            this.tuple = Tuple.Create(File.OpenRead(file1), File.OpenRead(file2));
        }

        public void Dispose()
        {
            this.tuple.Item1.Dispose();
            this.tuple.Item2.Dispose();
        }
    }
}".AssertReplace("Tuple.Create(File.OpenRead(file1), File.OpenRead(file2))", expression);

            RoslynAssert.Valid(Analyzer, code);
        }

        [TestCase("(File.OpenRead(file1), File.OpenRead(file2))")]
        public static void ValueTuple(string expression)
        {
            var code = @"
namespace N
{
    using System;
    using System.IO;

    public sealed class C : IDisposable
    {
        private readonly (FileStream, FileStream) tuple;

        public C(string file1, string file2)
        {
            this.tuple = (File.OpenRead(file1), File.OpenRead(file2));
        }

        public void Dispose()
        {
            this.tuple.Item1.Dispose();
            this.tuple.Item2.Dispose();
        }
    }
}".AssertReplace("(File.OpenRead(file1), File.OpenRead(file2))", expression);

            RoslynAssert.Valid(Analyzer, code);
        }

        [TestCase("Pair.Create(File.OpenRead(file1), File.OpenRead(file2))")]
        [TestCase("new Pair<FileStream>(File.OpenRead(file1), File.OpenRead(file2))")]
        public static void Pair(string expression)
        {
            var staticPairCode = @"
namespace N
{
    public static class Pair
    {
        public static Pair<T> Create<T>(T item1, T item2) => new Pair<T>(item1, item2);
    }
}";

            var genericPairCode = @"
namespace N
{
    public class Pair<T>
    {
        public Pair(T item1, T item2)
        {
            this.Item1 = item1;
            this.Item2 = item2;
        }

        public T Item1 { get; }

        public T Item2 { get; }
    }
}";

            var code = @"
namespace N
{
    using System;
    using System.IO;

    public sealed class C : IDisposable
    {
        private readonly Pair<FileStream> pair;

        public C(string file1, string file2)
        {
            this.pair = Pair.Create(File.OpenRead(file1), File.OpenRead(file2));
        }

        public void Dispose()
        {
            this.pair.Item1.Dispose();
            this.pair.Item2.Dispose();
        }
    }
}".AssertReplace("Pair.Create(File.OpenRead(file1), File.OpenRead(file2))", expression);

            RoslynAssert.Valid(Analyzer, genericPairCode, staticPairCode, code);
        }
    }
}
