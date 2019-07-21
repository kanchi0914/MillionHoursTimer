# MillionHoursTimer
## 概要
MillionHoursTimerは、指定したアプリケーションの起動時間を自動で記録してくれるWindows向け常駐アプリです。
各ファイル別に作業時間を記録することや、Web上でサマリーレポートを確認することが可能です。
時間管理や、モチベーションの維持等にお役立てください。

![image](./mainwindow.png?raw=true "clip_cmc")
![image](./toggle_summary.png?raw=true "toggl_summary")

##### 実行環境
.NET Framework 4.6

## 使い方
こちらからダウンロード後、適当な場所(Cドライブ直下など)に配置してください。
インストール不要で使えます。
MHTimer.exeから起動でき、最小化するか☓ボタンを押すとタスクトレイに常駐されます。
終了するときはタスクトレイのアイコンを右クリック>終了　をクリックしてください。
起動後に設定>システム起動時に自動的に起動　のチェックを入れることで、PC起動時に自動で起動されます。この場合、フォルダを移動すると設定が無効になるので注意してください。

### アプリの登録
記録したいアプリケーションの実行ファイル、またはショートカットファイルをドラッグ＆ドロップするか、
ファイル設定>アプリケーションの登録より、直接実行ファイルのパスを入力してください。
![image](./resistration.png?raw=true "registration")

### 計測方法の種類
起動時間の計測方法には3種類あり、自由に変更できます。
* 起動しているアプリを全て計測  
デフォルトの設定です。バックグラウンドで動作しているものも含め、起動している全てのアプリを計測対象に含めます。
* 最小化しているアプリを計測対象に含めない  
設定>記録設定>最小化しているアプリケーションを計測しない　から有効にできます。
最小化しているアプリを計測対象に含めないため、状況に応じて特定のアプリだけ記録を行うことが可能です。
* アクティブウィンドウのみを計測する  
設定>記録設定>アクティブウィンドウのみを計測する  　から有効にできます。
現在作業しているアプリのみ記録を行いたい場合に利用してください。ウィンドウを最小化した場合は記録されません。

### ファイル別作業時間について  
**タイトルバーにファイル名+拡張子が表示されるアプリケーション(Photoshopなど)**
に関しては、ファイル拡張子を設定することで、作業しているファイル別に作業時間を計測することが可能です。
リストからアプリ名を右クリック>ファイル拡張子の設定　をクリック後、
表示されるウィンドウにアプリケーションに応じた拡張子を半角スラッシュ区切りで設定してください。  
例)イラスト系ソフトの場合  
`.gif/.jpg/.jpeg/.png/.bmp/.ico/.tif/.tiff/.tga/.psd/.psb/.sai`  

現在のところ、上記の条件でファイル別作業時間の記録が可能なアプリケーションには以下のものを確認しています。
* イラスト、画像編集系ソフト  
Photoshop(CC 2018)、PaintTool SAI、Gimp
* エディタ系ソフト  
VS Code、Atom、Microsoft Word、IntelliJ、サクラエディタ、Terapad、TexStudio

それ以外でも、**タイトルバーにファイル名+ハイフン(-)が表示されるアプリケーション(Wordなど)**
に関しては、設定を変更することで記録が可能な場合があります。
その場合、設定>ファイル設定>ハイフン区切りをファイル名の取得に利用する　にチェックを入れてください。

なお、ファイル名でデータを区別しているため、途中でファイル名を変更すると別のデータとして保存されることに注意してください。

### Clip Studio Paintに関して
Clip Studio Paintでは仕様上、メインウィンドウのタイトルバーにファイル名が表示されないため、そのままではファイル名の取得ができません。この場合、以下の方法をとる必要があります。

* ページ管理ウィンドウをフローティングさせる方法  
漫画原稿などの複数ページ作品に関しては、
**「ページ管理」タブを、別ウィンドウで表示することで記録が可能**です。 この場合、上記「ハイフン区切りをファイル名の取得に利用する」にチェック後、ドラッグ＆ドロップで「ページ管理」タブをフローティングさせてください。
また一枚絵に関しても、**単ページの作品としてファイルを新規作成する**ことでこの方法をとることができます。  

![image](./clipstudiopaint_comic.png?raw=true "clip_cmc")

* ストーリーエディターをフローティングさせる方法  
複数ページで作品を作りたくない場合、「ストーリーエディター」タブをフローティングさせることで同様のことが可能です。この場合、記録されるファイル名は「作品名 ストーリーエディター」となるため、作品名のみを設定したい場合は設定>ファイル名をスペースで区切る　にチェックを入れてください。

なおいずれの場合も、記録できるのは作品ごとの作業時間に限られます。複数ページファイルのうち
特定のページのみ記録する方法に関しては、現在のところありません。


## Togglとの連携
時間管理用Webサービス、[Toggl](https://toggl.com)と連携することで、作業時間のサマリーレポートを自動で作成することが可能です。Toggleアカウントにログイン後、
Profile settings>API tokenをコピー、
設定>Toggl連携設定　のAPIキー欄に貼り付けてOKを押してください。
「認証が完了しました」と表示されればOKです。
認証がうまくいかない場合は、一度「Reset」をクリックしてみてください。

![image](./toggle_setting.png?raw=true "toggle")

認証完了後、下に表示されるアプリケーション一覧の「連携オン」にチェックを入れることで、アプリケーションの終了時に時間データをTogglに送信します。アプリケーション毎に「プロジェクト」と「タグ」を設定することが可能です。

## データのバックアップ、インポート
起動時間のデータはdataフォルダに保存されます。
バックアップをとる場合はdataフォルダをコピーし、
インポートする場合はdataフォルダをそのまま上書きしてください。

## 今後の開発予定
* リストの並び替え機能の実装
* インストーラーの作成

<!-- 
##

## 設定項目

* メインウィンドウ>右クリックメニュー
    * 表示アプリ名を変更  
    表示されるアプリ名を自由に設定することが可能です。
    * 表示内容をコピー  
    表示されているアプリ名と起動時間をクリップボードにコピーします。
    * ファイル別作業時間を確認  
    ファイル別作業時間一覧ウィンドウを開きます。
    * ファイル拡張子を設定  
    記録対象とするファイル拡張子を設定します。
    * 一覧から削除  
    アプリケーションのデータを削除します。記録は削除されるので、必要に応じてバックアップをとってください。
* 
-->

## License
MIT
