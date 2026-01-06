using System;
using System.Collections.Generic; // 【新規】List<T>、IEnumerable<T>などのコレクション型を利用するため追加
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Book.Chapter7.DomainEvents
{
    public class User
    {
        public int UserId { get; private set; }
        public string Email { get; private set; }
        public UserType Type { get; private set; }
        public bool IsEmailConfirmed { get; private set; }
        // 【新規】発生したドメイン・イベントを記録するためのリスト
        // コントローラ（UserController）は、このプロパティ経由でイベントを読み取り、外部システムに通知する。
        public List<EmailChangedEvent> EmailChangedEvents { get; private set; }

        public User(int userId, string email, UserType type, bool isEmailConfirmed)
        {
            UserId = userId;
            Email = email;
            Type = type;
            IsEmailConfirmed = isEmailConfirmed;
            // 【新規】イベントリストを初期化
            EmailChangedEvents = new List<EmailChangedEvent>();
        }

        public string CanChangeEmail()
        {
            if (IsEmailConfirmed)
                return "Can't change email after it's confirmed";

            return null;
        }

        public void ChangeEmail(string newEmail, Company company)
        {
            Precondition.Requires(CanChangeEmail() == null);

            // メールアドレスが変更されていない場合は処理を終了。
            // これにより、不必要なドメイン・イベントの生成とメッセージバスへの送信を防ぐ。
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
            // 【新規】Userオブジェクトの状態が実際に変更された（メールアドレスが変わった）直後に、
            // 変更内容を表すドメイン・イベントをリストに追加し、記録する。
            EmailChangedEvents.Add(new EmailChangedEvent(UserId, newEmail));
        }
    }

    /// <summary>
    /// アプリケーション・サービス（コントローラ）。
    /// ドメイン・イベントの仕組みを利用して、外部依存処理（メッセージ送信）を行う。
    /// </summary>
    public class UserController
    {
        private readonly Database _database = new Database();
        private readonly MessageBus _messageBus = new MessageBus();

        public string ChangeEmail(int userId, string newEmail)
        {
            object[] userData = _database.GetUserById(userId);
            User user = UserFactory.Create(userData);

            string error = user.CanChangeEmail();
            if (error != null)
                return error;

            object[] companyData = _database.GetCompany();
            Company company = CompanyFactory.Create(companyData);

            // ドメイン・ロジックの実行（この中でイベントが記録される可能性がある）
            user.ChangeEmail(newEmail, company);

            // データの永続化
            _database.SaveCompany(company);
            _database.SaveUser(user);

            // 【更新】ドメイン・イベントの処理。
            // Userが記録したすべてのイベントを反復処理し、メッセージバスに送信する。
            // これにより、メールアドレスが実際に変更された場合のみメッセージが送信されることが保証される。
            foreach (EmailChangedEvent ev in user.EmailChangedEvents)
            {
                _messageBus.SendEmailChangedMessage(ev.UserId, ev.NewEmail);
            }

            return "OK";
        }
    }

    /// <summary>
    /// 【新規】ドメイン・イベントを表す値オブジェクト。
    /// このクラスは不変（Immutable）であり、UserIdとNewEmailという「何が起こったか」の情報を持つ。
    /// </summary>
    public class EmailChangedEvent
    {
        // 変更後のユーザーを特定するためのID（イベントデータ）
        public int UserId { get; }
        // 変更後の新しいメールアドレス（イベントデータ）
        public string NewEmail { get; }

        /// <summary>
        /// ドメイン・イベントのコンストラクタ。
        /// イベントが持つべきデータを設定し、外部からの変更を防ぐためプロパティは読み取り専用とする。
        /// </summary>
        public EmailChangedEvent(int userId, string newEmail)
        {
            UserId = userId;
            NewEmail = newEmail;
        }

        // --- 値オブジェクトとしての比較を実装するオーバーライド ---
        
        /// <summary>
        /// 派生クラスまたは内部で利用される Equals 実装。
        /// 2つの EmailChangedEvent オブジェクトが「値」として等しいかどうかを判断する。
        /// ドメイン・イベントは値オブジェクトであるため、すべてのプロパティ（UserIdとNewEmail）が等しければ、
        /// 別のインスタンスであっても等しいと見なされる。
        /// </summary>
        protected bool Equals(EmailChangedEvent other)
        {
            // UserId と NewEmail の値が両方とも一致するかを確認する
            return UserId == other.UserId && string.Equals(NewEmail, other.NewEmail);
        }

        /// <summary>
        /// object から継承された Equals メソッドのオーバーライド。
        /// 外部からの比較要求（例: `eventA.Equals(eventB)`) を処理する。
        /// </summary>
        public override bool Equals(object obj)
        {
            // Nullチェック、参照の同一性チェック、型チェックを経て、最終的に protected Equals を呼び出す。
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return Equals((EmailChangedEvent)obj);
        }

        /// <summary>
        /// Equals メソッドをオーバーライドする場合に必須となる GetHashCode のオーバーライド。
        /// ハッシュコードは、Equals が true を返すオブジェクト同士で同じ値を返す必要がある。
        /// これにより、ハッシュテーブルや辞書などで正しくオブジェクトが扱われることが保証される。
        /// UserIdとNewEmailの値を組み合わせてユニークなハッシュコードを生成している。
        /// 
        /// 値オブジェクトについて、https://zenn.dev/yamachan0625/books/ddd-hands-on/viewer/chapter8_value_object
        /// この本の42, 43ページも参考になる。
        /// </summary>
        public override int GetHashCode()
        {
            // unchecked は、計算結果がその変数の型（この場合は int）の上限を超えてしまったときに、「エラー（例外）を出さずに、あふれた分を切り捨てて計算を続ける」 という指示
            unchecked
            {
                // 値オブジェクトの全プロパティのハッシュ値を組み合わせる
                // UserId と Email の組み合わせが1つでも違えば、全く違うハッシュ値（棚番号）になる
                return (UserId * 397) ^ (NewEmail != null ? NewEmail.GetHashCode() : 0);
            }
        }
    }

    public class UserFactory
    {
        public static User Create(object[] data)
        {
            return null;
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

    public class Tests
    {
        // 【更新】ドメイン・イベントが導入されたことで、単体テストはイベントが正しく生成されたかを確認する。
        [Fact]
        public void Changing_email_from_corporate_to_non_corporate()
        {
            var company = new Company("mycorp.com", 1);
            var sut = new User(1, "user@mycorp.com", UserType.Employee, false);

            sut.ChangeEmail("new@gmail.com", company);

            company.NumberOfEmployees.Should().Be(0);
            sut.Email.Should().Be("new@gmail.com");
            sut.Type.Should().Be(UserType.Customer);
            // 【新規】状態ベースのテスト：外部通信の代わりに、
            // Userオブジェクトが内部で正しいドメイン・イベントを記録したかを確認する。
            sut.EmailChangedEvents.Should().Equal(
                new EmailChangedEvent(1, "new@gmail.com"));
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
