# Laney VKUI for Avalonia

`Laney.VKUI.Avalonia` - библиотека VK-похожих контролов, тем, popup-обвязки и оконных примитивов для Avalonia-приложений. Код живет отдельно от бизнес-логики Laney: тут нет зависимостей на `ELOR.VKAPILib`, сессии VK, модели сообщений или сетевой слой.

## Что внутри

- `VKUITheme` с ресурсами цветов, типографики, иконок и VK Sans.
- Контролы из `VKUI.Controls`: `Avatar`, `Cell`, `NavigationControl`, `PanelHeader`, `Placeholder`, `Spinner`, `ToggleSwitch`, `VKIcon`, `VKUIFlyoutPresenter`, `WindowTitleBar`, `WindowNotificationManager`.
- Popup-слой из `VKUI.Popups`: `VKUIFlyout`, `ActionSheet`.
- Базовое окно диалога из `VKUI.Windows`.
- Утилиты раскладки фото из `VKUI.Utils`.

## Подключение

После публикации пакета:

```xml
<PackageReference Include="Laney.VKUI.Avalonia" Version="0.1.0-preview.1" />
```

До публикации пакета можно подключить проектом:

```xml
<ProjectReference Include="..\VKUI\VKUI.csproj" />
```

В `App.axaml` добавь тему:

```xml
<Application
    xmlns="https://github.com/avaloniaui"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:ui="using:VKUI">
    <Application.Styles>
        <ui:VKUITheme />
    </Application.Styles>
</Application>
```

Контролы подключаются обычным namespace:

```xml
xmlns:vkui="using:VKUI.Controls"
```

## Сборка и упаковка

```powershell
dotnet restore .\VKUI\VKUI.csproj
dotnet build .\VKUI\VKUI.csproj -c Release
dotnet pack .\VKUI\VKUI.csproj -c Release
```

Целевой framework сейчас `net10.0`, Avalonia - `12.0.4`. Да, это bleeding edge: библиотека следует за Laney, а не притворяется LTS-динозавром.

## Вынос в отдельный репозиторий

Из корня Laney:

```powershell
pwsh .\tools\Export-VKUI.ps1 -OutputPath ..\VKUI-Avalonia -InitGit -Force
```

Скрипт создает standalone-каталог с `VKUI`, минимальным `Directory.Packages.props`, root `README.md`, `.gitignore` и опционально делает `git init`. После этого каталог можно пушить в отдельный GitHub-репозиторий и публиковать пакет.

## Границы библиотеки

- Не добавлять сюда сетевой код, модели VK API и Laney-specific state.
- Новые контролы должны жить в `VKUI.Controls`, общие стили - в `Styles`, ресурсы темы - в `Appearance`.
- Если контрол требует данных Laney, значит он должен остаться в `L2`. Иначе опять получится общий ящик с проводами, только в красивой папке.
