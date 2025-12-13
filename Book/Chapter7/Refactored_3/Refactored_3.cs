using System;
using Xunit;

namespace Book.Chapter7.Refactored_3
{
    /// <summary>
    /// User エンティティ。会社の情報に関するロジックを Company クラスに完全に委譲し、
    /// 純粋なユーザーのドメイン・ロジックのみを保持する。
    /// </summary>
    public class User
    {
        public int UserId { get; private set; }
        public string Email { get; private set; }
        public UserType Type { get; private set; }

        public User(int userId, string email, UserType type)
        {
            UserId = userId;
            Email = email;
            Type = type;
        }

        /// <summary>
        /// ユーザーのメールアドレスを変更する。Company オブジェクトを引数として受け取り、
        /// 会社の従業員数変更の副作用を Company クラスに「命じる（Tell）」ことで実行する。
        /// 変更後の従業員数を戻り値として返さなくなった（void）。
        /// </summary>
        /// <param name="newEmail">新しいメールアドレス</param>
        /// <param name="company">協調者オブジェクトである Company インスタンス</param>
        public void ChangeEmail(string newEmail, Company company) // 【更新】引数が Company クラスに変更され、戻り値が void になった
        {
            if (Email == newEmail)
                return;

            // 【更新】メールアドレスが会社のドメインに属するかどうかを Company クラスに尋ねる
            UserType newType = company.IsEmailCorporate(newEmail) 
                ? UserType.Employee
                : UserType.Customer;

            if (Type != newType)
            {
                int delta = newType == UserType.Employee ? 1 : -1;
                // 【更新】従業員数の変更ロジックを Company クラスに委譲する（Tell, Don't Ask）
                company.ChangeNumberOfEmployees(delta);
            }

            Email = newEmail;
            Type = newType;
        }
    }

    public class UserFactory
    {
        public static User Create(object[] data)
        {
            Precondition.Requires(data.Length >= 3);

            int id = (int)data[0];
            string email = (string)data[1];
            UserType type = (UserType)data[2];

            return new User(id, email, type);
        }
    }

    /// <summary>
    /// アプリケーション・サービス（コントローラ）。外部依存処理の調整役。
    /// CompanyFactory を利用し、User オブジェクトと Company オブジェクトを操作する。
    /// </summary>
    public class UserController
    {
        private readonly Database _database = new Database();
        private readonly MessageBus _messageBus = new MessageBus();

        public void ChangeEmail(int userId, string newEmail)
        {
            object[] userData = _database.GetUserById(userId);
            User user = UserFactory.Create(userData);

            object[] companyData = _database.GetCompany();
            // 【更新】CompanyFactory を利用して Company オブジェクトを生成する
            Company company = CompanyFactory.Create(companyData);

            // 【更新】User.ChangeEmail に Company オブジェクトを渡す
            user.ChangeEmail(newEmail, company);

            // 【更新】データベースに Company オブジェクト自体を保存する
            _database.SaveCompany(company);
            _database.SaveUser(user);
            _messageBus.SendEmailChangedMessage(userId, newEmail);
        }
    }

    /// <summary>
    /// Company エンティティ。会社のデータ（ドメイン名、従業員数）と、
    /// それに関連するドメイン・ロジックをカプセル化する。
    /// </summary>
    public class Company
    {
        public string DomainName { get; private set; }
        public int NumberOfEmployees { get; private set; }

        public Company(string domainName, int numberOfEmployees)
        {
            DomainName = domainName;
            NumberOfEmployees = numberOfEmployees;
        }

        /// <summary>
        /// 会社の従業員数を変更する。
        /// 変更ロジック（従業員数の加算・減算）と、関連する制約（0未満にならないこと）をカプセル化する。
        /// </summary>
        /// <param name="delta">従業員数の増減量（+1 または -1）</param>
        public void ChangeNumberOfEmployees(int delta)
        {
            // 事前条件のチェック: 変更後の従業員数が0以上であることを保証する
            Precondition.Requires(NumberOfEmployees + delta >= 0, "Number of employees cannot be negative.");

            NumberOfEmployees += delta;
        }

        /// <summary>
        /// 指定されたメールアドレスが、この会社のドメインに属するかどうかを判定する。
        /// </summary>
        public bool IsEmailCorporate(string email)
        {
            string emailDomain = email.Split('@')[1];
            return emailDomain == DomainName;
        }
    }

    /// <summary>
    /// Company オブジェクトを生成するためのファクトリクラス。
    /// データベースの汎用的なデータ配列 (object[]) から Company オブジェクトに変換する責務を持つ。
    /// </summary>
    public class CompanyFactory
    {
        /// <summary>
        /// データベースのデータ配列から Company インスタンスを作成する。
        /// </summary>
        /// <param name="data">データベースから取得した会社情報を含む配列。
        /// 期待される要素は [0] string DomainName, [1] int NumberOfEmployees。</param>
        /// <returns>初期化された新しい Company インスタンス。</returns>
        public static Company Create(object[] data)
        {
            // データ配列の長さが Company オブジェクトを構成するのに十分であることを保証する
            Precondition.Requires(data.Length >= 2, "Company data array must contain at least 2 elements.");

            string domainName = (string)data[0];
            int numberOfEmployees = (int)data[1];

            return new Company(domainName, numberOfEmployees);
        }
    }

    public enum UserType
    {
        Customer = 1,
        Employee = 2
    }

    /// <summary>
    /// Company クラスの導入により、User クラスのロジックが Company クラスに分離されたため、
    /// 単体テストの対象が User クラスと Company クラスのそれぞれに分散される。
    /// 全てのテストが外部依存を持たないオブジェクトに対して行われるため、高速で信頼性が高い。
    /// </summary>
    public class Tests
    {
        [Fact]
        public void Changing_email_without_changing_user_type()
        {
            var company = new Company("mycorp.com", 1);
            var sut = new User(1, "user@mycorp.com", UserType.Employee);

            sut.ChangeEmail("new@mycorp.com", company);

            Assert.Equal(1, company.NumberOfEmployees);
            Assert.Equal("new@mycorp.com", sut.Email);
            Assert.Equal(UserType.Employee, sut.Type);
        }

        [Fact]
        public void Changing_email_from_corporate_to_non_corporate()
        {
            var company = new Company("mycorp.com", 1);
            var sut = new User(1, "user@mycorp.com", UserType.Employee);

            sut.ChangeEmail("new@gmail.com", company);

            Assert.Equal(0, company.NumberOfEmployees);
            Assert.Equal("new@gmail.com", sut.Email);
            Assert.Equal(UserType.Customer, sut.Type);
        }

        [Fact]
        public void Changing_email_from_non_corporate_to_corporate()
        {
            var company = new Company("mycorp.com", 1);
            var sut = new User(1, "user@gmail.com", UserType.Customer);

            sut.ChangeEmail("new@mycorp.com", company);

            Assert.Equal(2, company.NumberOfEmployees);
            Assert.Equal("new@mycorp.com", sut.Email);
            Assert.Equal(UserType.Employee, sut.Type);
        }

        [Fact]
        public void Changing_email_to_the_same_one()
        {
            var company = new Company("mycorp.com", 1);
            var sut = new User(1, "user@gmail.com", UserType.Customer);

            sut.ChangeEmail("user@gmail.com", company);

            Assert.Equal(1, company.NumberOfEmployees);
            Assert.Equal("user@gmail.com", sut.Email);
            Assert.Equal(UserType.Customer, sut.Type);
        }

        [InlineData("mycorp.com", "email@mycorp.com", true)]
        [InlineData("mycorp.com", "email@gmail.com", false)]
        [Theory]
        public void Differentiates_a_corporate_email_from_non_corporate(
            string domain, string email, bool expectedResult)
        {
            var sut = new Company(domain, 0);

            bool isEmailCorporate = sut.IsEmailCorporate(email);

            Assert.Equal(expectedResult, isEmailCorporate);
        }
    }

    public static class Precondition
    {
        public static void Requires(bool precondition, string message = null)
        {
            if (precondition == false)
                throw new Exception(message);
        }
    }

    public class Database
    {
        public object[] GetUserById(int userId)
        {
            return null;
        }

        public User GetUserByEmail(string email)
        {
            return null;
        }

        public void SaveUser(User user)
        {
        }

        public object[] GetCompany()
        {
            return null;
        }

        /// <summary>
        /// 【更新】Company オブジェクト全体を引数として受け取るように変更された。
        /// これにより、永続化の対象が従業員数（int）から Company エンティティに変わった。
        /// </summary>
        public void SaveCompany(Company company)
        {
        }
    }

    public class MessageBus
    {
        private IBus _bus;

        public void SendEmailChangedMessage(int userId, string newEmail)
        {
            _bus.Send($"Subject: USER; Type: EMAIL CHANGED; Id: {userId}; NewEmail: {newEmail}");
        }
    }

    internal interface IBus
    {
        void Send(string message);
    }
}