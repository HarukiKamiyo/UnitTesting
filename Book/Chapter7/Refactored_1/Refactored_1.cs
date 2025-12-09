namespace Book.Chapter7.Refactored_1
{
    /// <summary>
    /// ユーザーエンティティ。純粋なドメインロジックのみを保持し、
    /// 外部システム（DBやメッセージバス）への依存を一切持たない。
    /// </summary>
    public class User
    {
        // ユーザーの属性（状態）
        public int UserId { get; private set; }
        public string Email { get; private set; }
        public UserType Type { get; private set; }

        /// <summary>
        /// ユーザーオブジェクトを初期化するコンストラクタ。
        /// </summary>
        public User(int userId, string email, UserType type)
        {
            UserId = userId;
            Email = email;
            Type = type;
        }

        /// <summary>
        /// ユーザーのメールアドレスを変更するドメインロジックを実行する。
        /// データベースやメッセージバスなどの外部依存を排除するため、
        /// 必要な外部データは引数として受け取り、副作用（DB保存など）は行わず、
        /// 変更後の従業員数を戻り値として返す。
        /// </summary>
        /// <param name="newEmail">新しいメールアドレス</param>
        /// <param name="companyDomainName">会社のドメイン名（外部データ）</param>
        /// <param name="numberOfEmployees">現在の従業員数（外部データ）</param>
        /// <returns>メールアドレス変更によって計算された新しい従業員数</returns>
        public int ChangeEmail(string newEmail,
            string companyDomainName, int numberOfEmployees)
        {
            // 変更後のメールアドレスが現在のものと同じなら、何もしない
            if (Email == newEmail)
                return numberOfEmployees; // 従業員数も変更なし

            // 1. 新しいメールアドレスからドメイン名部分を抽出する
            string emailDomain = newEmail.Split('@')[1];

            // 2. 新しいメールアドレスが会社のドメイン名と一致するか判定する
            bool isEmailCorporate = emailDomain == companyDomainName;

            // 3. 判定結果に基づき、新しいユーザー種別を決定する（ドメインロジック）
            UserType newType = isEmailCorporate
                ? UserType.Employee // 会社のドメインなら従業員
                : UserType.Customer; // それ以外なら顧客

            // 4. ユーザー種別が変わるかチェックし、変わるなら従業員数を更新する
            if (Type != newType)
            {
                // 種別変更に伴う従業員数の増減量（従業員になるなら+1、顧客になるなら-1）
                int delta = newType == UserType.Employee ? 1 : -1;

                // 新しい従業員数を計算する
                int newNumber = numberOfEmployees + delta;

                // 戻り値として返すために、引数の変数を更新（ローカル変数に格納しているだけ）
                numberOfEmployees = newNumber;
            }

            // 5. ユーザーオブジェクトの状態を新しい値に更新する（ドメインロジック）
            Email = newEmail;
            Type = newType;

            // 6. 最終的に計算された従業員数を呼び出し元（UserController）に返す
            return numberOfEmployees;
        }
    }

    /// <summary>
    /// アプリケーション・サービス層、またはコントローラ（Humble Object）。
    /// ドメインロジック外の処理（入出力、外部システム通信）を担当する。
    /// ドメイン（User）に対して依存性のないデータを提供し、ドメインの実行結果を受け取り、
    /// その結果に基づいて外部システムを操作する。
    /// </summary>
    public class UserController
    {
        // 依存オブジェクト（外部システムへのアダプター）をフィールドとして保持
        private readonly Database _database = new Database();
        private readonly MessageBus _messageBus = new MessageBus();

        /// <summary>
        /// 外部依存を伴う、メールアドレス変更のプロセス全体を制御するメソッド。
        /// </summary>
        public void ChangeEmail(int userId, string newEmail)
        {
            // 【依存処理 1: データベース読み込み】
            // データベースから既存のユーザー情報（メールアドレスと種別）を読み込む
            object[] data = _database.GetUserById(userId);
            string email = (string)data[1];
            UserType type = (UserType)data[2];

            // 読み込んだ情報でUserエンティティを生成（ドメインオブジェクトの生成）
            var user = new User(userId, email, type);

            // 【依存処理 2: データベース読み込み】
            // データベースから会社の情報（ドメイン名と現在の従業員数）を読み込む
            object[] companyData = _database.GetCompany();
            string companyDomainName = (string)companyData[0];
            int numberOfEmployees = (int)companyData[1];

            // 【ドメインロジックの実行】
            // UserエンティティのChangeEmailメソッドを実行する。
            // 依存オブジェクトから取得したデータを引数として渡す。
            int newNumberOfEmployees = user.ChangeEmail(
                newEmail, companyDomainName, numberOfEmployees);

            // ドメインロジックの実行結果（Userオブジェクトの状態と新しい従業員数）に基づき、
            // 外部システム（データベース、メッセージバス）への永続化/通知を行う。

            // 【依存処理 3: データベース保存】
            // データベースに新しい従業員数を保存する（会社の状態を更新）
            _database.SaveCompany(newNumberOfEmployees);

            // 【依存処理 4: データベース保存】
            // データベースに変更後のユーザー情報（EmailとType）を保存する
            _database.SaveUser(user);

            // 【依存処理 5: 外部通知】
            // 外部システムへメールアドレスが変更されたことを通知するメッセージを送信する
            _messageBus.SendEmailChangedMessage(userId, newEmail);
        }
    }

    // ユーザー種別を表す列挙型
    public enum UserType
    {
        Customer = 1,
        Employee = 2
    }

    /// <summary>
    /// データベースへのアクセスを模倣したアダプタークラス。
    /// 静的メソッドからインスタンスメソッドに変更されている。
    /// </summary>
    public class Database
    {
        public object[] GetUserById(int userId) { return null; }
        public User GetUserByEmail(string email) { return null; }
        public void SaveUser(User user) { }
        public object[] GetCompany() { return null; }
        public void SaveCompany(int newNumber) { }
    }

    /// <summary>
    /// メッセージバスへの通信を模倣したアダプタークラス。
    /// 静的メソッドからインスタンスメソッドに変更されている。
    /// </summary>
    public class MessageBus
    {
        // 実際のメッセージ送信インターフェースはコンストラクタでインジェクションされる想定（ここではフィールド定義のみ）
        private IBus _bus;

        public void SendEmailChangedMessage(int userId, string newEmail)
        {
            // 内部のバスオブジェクトを使ってメッセージを送信する
            // nullチェックなどの実装は省略
            //_bus.Send($"Subject: USER; Type: EMAIL CHANGED; Id: {userId}; NewEmail: {newEmail}");
        }
    }

    // メッセージ送信機能のインターフェース
    internal interface IBus
    {
        void Send(string message);
    }
}