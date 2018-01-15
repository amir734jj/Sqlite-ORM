using System;

namespace Sqlite.ORM.Tests
{
    public static class DateTimeExtension {
        
        /// <summary>
        /// The is needed, because ToString of DateTime does not include the millisecond portion,
        /// hence, we trimmed the millisecond before equality check
        /// </summary>
        /// <param name="dt"></param>
        /// <returns></returns>
        public static DateTime TrimMilliseconds(this DateTime dt)
        {
            return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, 0);
        }
    }    
    
    public class DummyNestedTestClass : IEquatable<DummyNestedTestClass>{
        public string MotherName { get; set; }
        public string FatherName { get; set; }
        public bool Status { get; set; }

        public bool Equals(DummyNestedTestClass other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(MotherName, other.MotherName)
                   && string.Equals(FatherName, other.FatherName)
                   && Status == other.Status;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((DummyNestedTestClass) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (MotherName != null ? MotherName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (FatherName != null ? FatherName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ Status.GetHashCode();
                return hashCode;
            }
        }
    }
    
    public class DummyTestClass : IEquatable<DummyTestClass>
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public int Age { get; set; }
        public double Height { get; set; }
        public float Worth { get; set; }
        public long Weight { get; set; }
        public DateTime DateOfBirth { get; set; }
        public char Initial { get; set; }
        public DummyNestedTestClass Parents { get; set; }
        public ObjectId IdNumber { get; set; }

        public bool Equals(DummyTestClass other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(FirstName, other.FirstName)
                   && string.Equals(LastName, other.LastName)
                   && Age == other.Age && Height.Equals(other.Height)
                   && Worth.Equals(other.Worth)
                   && Weight == other.Weight
                   && DateOfBirth.TrimMilliseconds().Equals(other.DateOfBirth.TrimMilliseconds())
                   && Initial == other.Initial
                   && Equals(Parents, other.Parents)
                   && IdNumber.Equals(other.IdNumber);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((DummyTestClass) obj);
        }
    }
}