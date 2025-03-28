## *Помощь проекту*
Автор будет благодарен за любую поддержку. Реквизиты указаны по [ссылке](https://andreyabdulkayumov.github.io/TerminalProgram_Website/donate.html).

## *Статьи на Хабр*

[Кроссплатформенный терминал Modbus TCP / RTU / ASCII с открытым исходным кодом: Часть 3](https://habr.com/ru/articles/871788/)

[Кроссплатформенный терминал Modbus TCP / RTU / ASCII с открытым исходным кодом: Часть 2](https://habr.com/ru/articles/854824/)

[Терминал Modbus TCP / RTU / ASCII с открытым исходным кодом: Часть 1](https://habr.com/ru/articles/795387/)

## *История версий*

Ссылки на скачивание любой публичной версии приложения вы можете найти на [этой странице](https://andreyabdulkayumov.github.io/TerminalProgram_Website/downloads.html).

## *Терминальная программа*
Начиная с версии 3.0.0 в проекте используется Avalonia UI, в более ранних версиях WPF.

Платформа:
- .NET Framework до версии 1.9.1 включительно.
- .NET 7 начиная с версии 1.10.0.
- .NET 8 начиная с версии 2.3.0.
- .NET 9 начиная с версии 3.0.0.

Приложение может выступать в роли *IP* и *SerialPort* клиента. Выбор типа клиента происходит в меню настроек.

Поддерживается два типовых режима работы:
1. Обмен данными по *стандартным* протоколам, которые поддерживает .NET.
2. Обмен данными по *специальным* протоколам.

Приложение поддерживает ***Темную*** и ***Светлую*** тему оформления.

# Режимы работы

Подробнее о приложении можно узнать из статей на Хабр или из встроенного руководства пользователя (кнопка с вопросом в верхнем левом углу). 

## *"Без протокола"*
В поле передачи пользователь пишет данные, которые нужно отправить. В поле приема находятся данные, которые прислал сервер или внешнее устройство. 

	Поддерживаются протоколы: 
	- UART
	- TCP

<p align="center">
  <img src="https://github.com/user-attachments/assets/1cecbd44-6c89-463d-bbc1-ffeedf281db6"/>
</p>

<p align="center">
  <img src="https://github.com/user-attachments/assets/e25536d1-977e-42a3-a864-cd6b452dbd71"/>
</p>

## *"Modbus"*
Пользователь может взаимодействовать с выбранными регистрами Modbus, используя соответствующие элементы интерфейса. Для дополнительной расшифровки транзакции существует раздел с представлениями.

	Поддерживаются протоколы: 
	- Modbus TCP
	- Modbus RTU
 	- Modbus ASCII
  	- Modbus RTU over TCP
 	- Modbus ASCII over TCP

<p align="center">
  <img src="https://github.com/user-attachments/assets/9548b19c-19fb-4be5-8915-d344a4eea920"/>
</p>

<p align="center">
  <img src="https://github.com/user-attachments/assets/e8045c3e-f4b4-4001-952e-8fd2ba04dc94"/>
</p>

## *"Макросы"*

Этот режим позволяет работать с макросами для режимов "Без протокола" и "Modbus". При наведении курсора на макрос появляются кнопки редактирования и удаления.

<p align="center">
  <img src="https://github.com/user-attachments/assets/89928b5b-eb8f-4073-9c18-98aa3dd82811"/>
</p>

<p align="center">
  <img src="https://github.com/user-attachments/assets/d0b2bd02-2010-4d63-b7d0-102ccb982f58"/>
</p>

# *Вспомогательный софт*
GUI Framework - [Avalonia UI](https://avaloniaui.net/)

Для упрощения работы с паттерном MVVM использован [ReactiveUI](https://www.reactiveui.net/)

Для тестирования используется [xUnit](https://xunit.net/)

Скрипт установщика написан с помощью [Inno Setup Compiler](https://jrsoftware.org/isdl.php)

Иконки приложения [Material.Icons.Avalonia](https://github.com/AvaloniaUtils/Material.Icons.Avalonia/)

# *Система версирования* Global.Major.Minor

*Global* - глобальная версия репозитория. До релиза это 0. Цифра меняется во время релиза и при именениях, затрагивающих значительную часть UI или внутренней логики.

*Major* - добавление нового функционала, крупные изменения.

*Minor* - исправление багов, мелкие добавления.
