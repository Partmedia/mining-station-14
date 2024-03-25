(quicklisp:quickload "cl-yaml")
(quicklisp:quickload "local-time")
(quicklisp:quickload "str")

(defun changes-since (date)
  (str:lines (uiop:run-program (format nil "git log --pretty=format:'%s' --since=~a" date) :output :string)))

(defun last-entry (file)
  (first (last (gethash "Entries" (yaml:parse file)))))

(defun filter (line)
  (remove #\] (remove #\[ line)))

(defun format-next (last-id last-date)
  (format t "- author: Mining Station 14 Team~%  changes:~%")
  (dolist (change (changes-since last-date))
    (format t "  - { type: Add, message: ~a }~%" (yaml:emit-to-string (filter change))))
  (format t "  id: ~a~%" (1+ last-id))
  (format t "  time: '~a'" (local-time:now)))

(defun gen-cl (file)
  (let ((prev (last-entry file)))
    (with-output-to-string (*standard-output*)
      (format-next (gethash "id" prev) (gethash "time" prev)))))

(defun update-cl (file)
  (let ((str (gen-cl file)))
    (with-open-file (f file :direction :output :if-exists :append)
      (format f "~a~%" str))))

(update-cl #P"Resources/Changelog/DungeonChangelog.yml")
(exit)
