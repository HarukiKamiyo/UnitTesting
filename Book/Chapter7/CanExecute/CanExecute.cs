using System;
using Xunit;

namespace Book.Chapter7.CanExecute
{
    /// <summary>
    /// User エンティティ。CanExecute/Execute パターンを実装し、
    /// 操作の可否を判断するビジネス・ロジック（決定を下す処理）を自身の中にカプセル化する。
    /// </summary>
    public class User
    {
        public int UserId { get; private set; }
        public string Email { get; private set; }
        public UserType Type { get; private set; }
        // 【追加】メールアドレスが確定済みかを示す新しい状態
        public bool IsEmailConfirmed { get; private set; }

        /// <summary>
        /// 【更新】コンストラクタに IsEmailConfirmed の初期値が追加された。
        /// </summary>
        public User(int userId, string email, UserType type, bool isEmailConfirmed)
        {
            UserId = userId;
            Email = email;
            Type = type;
            IsEmailConfirmed = isEmailConfirmed;
        }

        /// <summary>
        /// 【新規】確認メソッド（CanExecute）。メールアドレスの変更が可能かどうかを判断する。
        /// 変更不可能な場合はエラーメッセージ（文字列）を返し、可能な場合は null を返す。
        /// これにより、コントローラから決定を下す処理を分離する。
        /// </summary>
        public string CanChangeEmail()
        {
            if (IsEmailConfirmed)
                return "Can't change email after it's confirmed";

            return null;
        }

        /// <summary>
        /// 【更新】実行メソッド（Execute）。処理を開始する前に、CanChangeEmail() が成功した（null を返した）
        /// ことを Precondition で検証する。これにより、このメソッド自体が操作の正当性を保証する。
        /// </summary>
        public void ChangeEmail(string newEmail, Company company)
        {
            // 【更新】事前条件チェック。コントローラが CanChangeEmail() を呼び出し、
            // 変更可能と判断した場合のみ、このメソッドが実行されることを保証する。
            Precondition.Requires(CanChangeEmail() == null);

            if (Email == newEmail)
                return;

            UserType newType = company.IsEmailCorporate(newEmail)
                ? UserType.Employee
                : UserType.Customer;

            if (Type != newType)
            {
                int delta = newType == UserType.Employee ? 1 : -1;
                company.ChangeNumberOfEmployees(delta);
            }

            Email = newEmail;
            Type = newType;
        }
    }

    public class UserFactory
    {
        // 変更なし（Create メソッドの実装は省略されているが、User オブジェクトの生成ロジックを担う）
        public static User Create(object[] data)
        {
            return null;
        }
    }

    /// <summary>
    /// アプリケーション・サービス（コントローラ）。外部依存処理の調整役。
    /// CanExecute パターンに従い、ドメイン・モデルに操作の可否を尋ねるのみで、
    /// 自身ではビジネス的な決定を下さない（Humble Objectの原則を徹底）。
    /// </summary>
    public class UserController
    {
        private readonly Database _database = new Database();
        private readonly MessageBus _messageBus = new MessageBus();

        /// <summary>
        /// 【更新】メソッドの戻り値が string（エラーメッセージまたは"OK"）に変更された。
        /// これは、操作が成功したか失敗したかを呼び出し元に伝えるためである（Resultクラスの代わり）。
        /// </summary>
        public string ChangeEmail(int userId, string newEmail)
        {
            // データの準備
            object[] userData = _database.GetUserById(userId);
            User user = UserFactory.Create(userData);

            // 【更新】決定を下す前に、ドメイン・モデル（User）に操作の可否を尋ねる（CanExecuteの呼び出し）
            string error = user.CanChangeEmail();

            // 【更新】失敗した場合、コントローラは調整役として処理を中断し、エラーメッセージを返す。
            // ここでコントローラは「何が間違いか」という決定を下していない。
            if (error != null)
                return error;

            // 成功した場合のみ、残りのプロセス（データの取得、実行、永続化）に進む
            object[] companyData = _database.GetCompany();
            Company company = CompanyFactory.Create(companyData);

            // 実行メソッド（Execute）を呼び出す
            user.ChangeEmail(newEmail, company);

            // データの永続化と外部通知
            _database.SaveCompany(company);
            _database.SaveUser(user);
            _messageBus.SendEmailChangedMessage(userId, newEmail);

            return "OK";
        }
    }

    public class Company
    {
        public string DomainName { get; private set; }
        public int NumberOfEmployees { get; private set; }

        public Company(string domainName, int numberOfEmployees)
        {
            DomainName = domainName;
            NumberOfEmployees = numberOfEmployees;
        }

        public void ChangeNumberOfEmployees(int delta)
        {
            Precondition.Requires(NumberOfEmployees + delta >= 0);

            NumberOfEmployees += delta;
        }

        public bool IsEmailCorporate(string email)
        {
            string emailDomain = email.Split('@')[1];
            return emailDomain == DomainName;
        }
    }

    public class CompanyFactory
    {
        public static Company Create(object[] data)
        {
            Precondition.Requires(data.Length >= 2);

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
