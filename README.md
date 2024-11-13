# TerminalProgram

## *Статьи на Хабр*

Терминал Modbus TCP / RTU / ASCII с открытым исходным кодом: Часть 1

https://habr.com/ru/articles/795387/

## *Терминальная программа*
Начиная с версии 3.0.0 в проекте используется Avalonia UI, в более ранних версиях WPF.

Платформа:
- .NET Framework до версии 1.9.1 включительно.
- .NET 7 начиная с версии 1.10.0.
- .NET 8 начиная с версии 2.3.0.
- .NET 9 начиная с версии 3.0.0.

Данная программа может выступать в роли *IP* и *SerialPort* клиента. Выбор типа клиента происходит в меню настроек.

Поддерживается два типовых режима работы:
1. Обмен данными по *стандартным* протоколам, которые поддерживает .NET.
2. Обмен данными по *специальным* протоколам.

Приложение поддерживает следующие темы оформления:
1. Темная.
2. Светлая.

# Краткое описание режимов работы
## *"Без протокола"*
В поле передачи пользователь пишет данные, которые нужно отправить. В поле приема находятся данные, которые прислал сервер или внешнее устройство.

	Поддерживаются протоколы: 
	- SerialPort (UART)
	- TCP

<p align="center">
  <img src="https://github.com/user-attachments/assets/abafe38b-fe23-45d4-87bf-ad9c16c2453a"/>
</p>

<p align="center">
  <img src="https://github.com/user-attachments/assets/730c45ef-106a-4a69-85fe-75880ec3d3f3"/>
</p>

## *"Modbus"*
Пользователь может взаимодействовать с выбранными регистрами Modbus в соответствующих полях. История действий отображается в таблице. 
В полях "Запрос" и "Ответ" отображается последняя транзакция. Содержимое этих пакетов выводится в байтах.

	Поддерживаются протоколы: 
	- Modbus TCP
	- Modbus RTU
 	- Modbus ASCII

<p align="center">
  <img src="https://github.com/user-attachments/assets/00f85b38-ac78-453d-b3d6-b36a68c26afd"/>
</p>

<p align="center">
  <img src="https://github.com/user-attachments/assets/20cb7161-e1dc-43aa-b1c9-6eabea093ccb"/>
</p>

# Индикация

Индикация приема и передачи обеспечивает визуальный контроль текущей активности. 
Это помогает не только в мониторинге обмена данных, но и улучшает опыт пользователя при взаимодействии с подключенными устройствами. 

При получении и отправке данных мигают соответствующие индикаторы.

	Используется в режимах: 
	- "Без протокола"
	- "Modbus"
 
<p align="center">
  <img src="https://github.com/AndreyAbdulkayumov/TerminalProgram/assets/86914394/de31a90c-b252-4a95-a526-c5236249560e"/>
</p>

# Цикличный опрос

Суть этой возможности заключается в том, что через заданный промежуток времени на хост отправляется сообщение. 
Но важно помнить, что каждый режим накладывает некоторые ограничения на работу цикличного опроса.

Эта возможность доступна для следующих режимов работы:

## *"Без протокола"*

В поле ввода пользователь вводит сообщение, которое будет отправляться на хост с заданным периодом.
При этом не важно ответит ли хост или нет.

Ответ хоста можно "запаковать" в диагностические данные. 
Формат отображаемой строки настраивается с помощью выставления соответствующих галочек.

## *"Modbus"*

В режиме "Modbus" при цикличном опросе возможно использование только функций чтения. 
При этом, если хост не ответит за указанный в настройках таймаут, то опрос прекратится.

Если в качестве интерфейса связи выбран последовательный порт, то выбор между протоколами Modbus ASCII и Modbus RTU происходит в главном окне.


# *Вспомогательный софт*
GUI Framework - Avalonia UI

https://avaloniaui.net/

Для упрощения работы с паттерном MVVM использован ReactiveUI

https://www.reactiveui.net/

Для тестирования используется xUnit

https://xunit.net/

Скрипт установщика написан с помощью Inno Setup Compiler 6.2.2

https://jrsoftware.org/isdl.php

# *Система версирования* Global.Major.Minor

*Global* - глобальная версия репозитория. До релиза это 0. Цифра меняется во время релиза и при именениях, затрагивающих значительную часть UI или внутренней логики.

*Major* - добавление нового функционала, крупные изменения.

*Minor* - исправление багов, мелкие добавления.
