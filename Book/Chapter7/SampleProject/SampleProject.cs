namespace Book.Chapter7.SampleProject
{
    public class User
    {
        // ユーザーID、メールアドレス、ユーザー種別を保持する
        public int UserId { get; private set; }
        public string Email { get; private set; }
        public UserType Type { get; private set; }

        /// <summary>
        /// ユーザーのメールアドレスを変更し、関連する業務ロジックを実行するメソッド。
        /// </summary>
        /// <param name="userId">メールアドレスを変更するユーザーのID</param>
        /// <param name="newEmail">新しいメールアドレス</param>
        public void ChangeEmail(int userId, string newEmail)
        {
            // 【依存: データベース】
            // データベースから既存のユーザー情報（メールアドレスと種別）を読み込む
            object[] data = Database.GetUserById(userId);

            // 読み込んだ情報を使って、現在のユーザーオブジェクトのプロパティを初期化する
            UserId = userId;
            Email = (string)data[1]; // 読み込んだメールアドレスを設定
            Type = (UserType)data[2]; // 読み込んだユーザー種別を設定

            // 変更後のメールアドレスが現在のものと同じなら、処理を終了する
            if (Email == newEmail)
                return;

            // （コメントアウト：重複チェックの処理）
            //bool isEmailTaken = Database.GetUserByEmail(newEmail) != null;
            //if (isEmailTaken)
            //    return "Email is taken";

            // 【依存: データベース】
            // データベースから会社の情報（ドメイン名と現在の従業員数）を読み込む
            object[] companyData = Database.GetCompany();
            string companyDomainName = (string)companyData[0];
            int numberOfEmployees = (int)companyData[1]; // 現在の従業員数

            // 新しいメールアドレスからドメイン名部分を抽出する
            string emailDomain = newEmail.Split('@')[1];

            // 新しいメールアドレスが会社のドメイン名と一致するか判定する
            bool isEmailCorporate = emailDomain == companyDomainName;

            // 判定結果に基づき、新しいユーザー種別を決定する
            UserType newType = isEmailCorporate
                ? UserType.Employee // 会社のドメインなら従業員
                : UserType.Customer; // それ以外なら顧客

            // ユーザー種別が変わるかチェックする
            if (Type != newType)
            {
                // 種別変更に伴う従業員数の増減量（従業員になるなら+1、顧客になるなら-1）
                int delta = newType == UserType.Employee ? 1 : -1;
                // 新しい従業員数を計算する
                int newNumber = numberOfEmployees + delta;

                // 【依存: データベース】
                // データベースに新しい従業員数を保存する（会社の状態を更新）
                Database.SaveCompany(newNumber);
            }

            // ユーザーオブジェクトのメールアドレスと種別を新しい値に更新する
            Email = newEmail;
            Type = newType;

            // 【依存: データベース】
            // データベースに変更後のユーザー情報を保存する
            Database.SaveUser(this);

            // 【依存: メッセージバス】
            // 外部システムへメールアドレスが変更されたことを通知するメッセージを送信する
            MessageBus.SendEmailChangedMessage(UserId, newEmail);
        }
    }

    // ユーザー種別を表す列挙型
    public enum UserType
    {
        Customer = 1,
        Employee = 2
    }

    // データベースへのアクセスを模倣した静的クラス（すべてのメソッドが外部依存）
    public class Database
    {
        // ユーザーIDでユーザー情報を取得する
        public static object[] GetUserById(int userId) { return null; }
        // メールアドレスでユーザー情報を取得する
        public static User GetUserByEmail(string email) { return null; }
        // ユーザー情報をデータベースに保存する
        public static void SaveUser(User user) { }
        // 会社情報を取得する（ドメイン名と従業員数が含まれる）
        public static object[] GetCompany() { return null; }
        // 新しい従業員数をデータベースに保存する
        public static void SaveCompany(int newNumber) { }
    }

    // メッセージバスへの通信を模倣した静的クラス（外部依存）
    public class MessageBus
    {
        private static IBus _bus; // 実際にメッセージを送信するインターフェースのインスタンス

        // メールアドレス変更メッセージを外部に送信する
        public static void SendEmailChangedMessage(int userId, string newEmail)
        {
            // 内部のバスオブジェクトを使ってメッセージを送信する
            _bus.Send($"Subject: USER; Type: EMAIL CHANGED; Id: {userId}; NewEmail: {newEmail}");
        }
    }

    // メッセージ送信機能のインターフェース
    internal interface IBus
    {
        void Send(string message);
    }
}