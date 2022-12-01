 (module
  (import "imports" "mem" (memory 1))
  (global (export "MonoWebCilVersion") i32 (i32.const 0))
  (global $MonoWebCilDescriptorLength (export "MonoWebCilDescriptorLength") i32 (i32.const 2))
  (data $X "\01\02") ;; $MonoWebCilDescriptor
  (global $MonoWebCilModule0Length i32 (i32.const 4))
  (data $Y "MZ\01\02") ;; $MonoWebCilModule0
  (func (export "MonoWebCilGetDescriptor") (param i32 i32) (result)
    (if (i32.eq (local.get 1) (global.get $MonoWebCilDescriptorLength))
      (memory.init $X ;; $MonoWebCilDescriptor
        (local.get 0) ;;  dest
        (i32.const 0) ;; src
        (global.get $MonoWebCilDescriptorLength) ;; n
      )
    )
  )
  (func (export "MonoWebCilGetModuleData") (param i32 i32 i32) (result)
    (if (i32.eq (local.get 0) (i32.const 0))
      (if (i32.eq (local.get 2) (global.get $MonoWebCilModule0Length))
        (memory.init $Y ;; $MonoWebCilModule0
          (local.get 1) ;; dest
          (i32.const 0) ;; src
          (global.get $MonoWebCilModule0Length) ;; n
        )
      )
    )
    ;; (if (== moduleIdx) 1) (memory.fill $MonoWebCilModule1Length))
    ;; (if (== moduleIdx) 2) ...)
    ;; ...
  )
)
