whitelist-not-whitelisted = Вас нет в вайтлисте.
whitelist-not-whitelisted-rp = Вас нет в вайтлисте. Чтобы попасть в вайтлист, посетите наш Discord (ссылку можно найти по адресу https://discord.station14.ru).
command-whitelistadd-description = Добавить игрока с указанным юзернеймом в вайтлист.
command-whitelistadd-help = whitelistadd <username>
command-whitelistadd-existing = { $username } уже в вайтлисте!
command-whitelistadd-added = { $username } добавлен в вайтлист
command-whitelistadd-not-found = Пользователь '{ $username }' не найден
command-whitelistremove-description = Удалить игрока с указанным юзернеймом из вайтлиста.
command-whitelistremove-help = whitelistremove <username>
command-whitelistremove-existing = { $username } не в вайтлисте!
command-whitelistremove-removed = Пользователь { $username } удалён из вайтлиста
command-whitelistremove-not-found = Пользователь '{ $username }' не найден
command-kicknonwhitelisted-description = Кикнуть с сервера всех пользователей не из вайтлиста.
command-kicknonwhitelisted-help = kicknonwhitelisted
ban-banned-permanent = Этот бан можно только обжаловать. Для этого посетите наш Discord: https://discord.ss14.ru
ban-expires = Вы получили бан на { $duration } минут, и он истечёт { $time } по UTC (для москосвкого времени добавьте 3 часа).
ban-banned-1 = Вам, или другому пользователю этого компьютера или соединения, запрещено здесь играть.
ban-banned-2 = Причина бана: "{ $reason }"
soft-player-cap-full = Сервер заполнен!
panic-bunker-account-denied = Этот сервер находится в режиме "Бункер". В данный момент новые подключения не принимаются. Повторите попытку позже
panic-bunker-account-denied-reason = Этот сервер находится в режиме "Бункер", и вам было отказано в доступе. Причина: "{ $reason }"
panic-bunker-account-reason-account = Ваш аккаунт должен быть старше { $minutes } минут
panic-bunker-account-reason-overall =
    Необходимо минимальное отыгранное время — { $hours } { $hours ->
        [one] час
        [few] часа
       *[other] часов
    }.
