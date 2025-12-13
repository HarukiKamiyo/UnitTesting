using System;

namespace Book.Chapter7.Refactored_2
{
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

        public int ChangeEmail(string newEmail,
            string companyDomainName, int numberOfEmployees)
        {
            if (Email == newEmail)
                return numberOfEmployees;

            string emailDomain = newEmail.Split('@')[1];
            bool isEmailCorporate = emailDomain == companyDomainName;
            UserType newType = isEmailCorporate
                ? UserType.Employee
                : UserType.Customer;

            if (Type != newType)
            {
                int delta = newType == UserType.Employee ? 1 : -1;
                int newNumber = numberOfEmployees + delta;
                numberOfEmployees = newNumber;
            }

            Email = newEmail;
            Type = newType;

            return numberOfEmployees;
        }
    }

    /// <summary>
    /// ファクトリクラス。データベースから取得した汎用的なデータ（object[]）を
    /// ドメイン・クラスである User オブジェクトに変換する責務を持つ。
    /// UserController からデータ変換の複雑さ（インデックス参照や型キャスト）を分離し、
    /// この変換ロジックを単独でテスト可能にする（Humble Object パターンの一種）。
    /// </summary>
    public class UserFactory
    {
        /// <summary>
        /// データベースのデータ配列から新しい User インスタンスを作成する。
        /// </summary>
        /// <param name="data">データベースから取得したユーザー情報を含む配列。
        /// 期待される要素は [0] int ID, [1] string Email, [2] UserType Type。</param>
        /// <returns>初期化された新しい User インスタンス。</returns>
        public static User Create(object[] data)
        {
            // データ配列の長さが User オブジェクトを構成するのに十分であることを保証する。
            // (ID, Email, Typeの3要素を想定)
            Precondition.Requires(data.Length >= 3, "User data array must contain at least 3 elements.");

            // インデックス参照と型キャストを行い、User オブジェクトに必要な値を取り出す。
            int id = (int)data[0];
            string email = (string)data[1];
            UserType type = (UserType)data[2];

            // User ドメイン・オブジェクトを生成して返す。
            return new User(id, email, type);
        }
    }

    public class UserController
    {
        private readonly Database _database = new Database();
        private readonly MessageBus _messageBus = new MessageBus();

        public void ChangeEmail(int userId, string newEmail)
        {
            object[] userData = _database.GetUserById(userId);
            User user = UserFactory.Create(userData);

            object[] companyData = _database.GetCompany();
            string companyDomainName = (string)companyData[0];
            int numberOfEmployees = (int)companyData[1];

            int newNumberOfEmployees = user.ChangeEmail(
                newEmail, companyDomainName, numberOfEmployees);

            _database.SaveCompany(newNumberOfEmployees);
            _database.SaveUser(user);
            _messageBus.SendEmailChangedMessage(userId, newEmail);
        }
    }

    public enum UserType
    {
        Customer = 1,
        Employee = 2
    }

    /// <summary>
    /// 事前条件（Precondition）をチェックするための静的ユーティリティクラス。
    /// メソッドが処理を開始するために必要な条件（ガード節）を満たしているかを検証する。
    /// 条件が偽（false）の場合、例外をスローし、不正な入力による処理の継続を防ぐ。
    /// </summary>
    public static class Precondition
    {
        /// <summary>
        /// 指定された条件が満たされているか検証する。
        /// </summary>
        /// <param name="precondition">検証する真偽値。true なら処理続行、false なら例外スロー。</param>
        /// <param name="message">条件が満たされなかった場合にスローされる例外メッセージ。</param>
        public static void Requires(bool precondition, string message = null)
        {
            // 条件が偽（false）の場合に例外をスローする。
            if (precondition == false)
                throw new Exception(message ?? "Precondition failed.");
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

        public void SaveCompany(int newNumber)
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
