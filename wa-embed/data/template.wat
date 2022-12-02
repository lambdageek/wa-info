 (module
  (import "imports" "mem" (memory 1))
  (global (export "MonoWebCilVersion") i32 (i32.const 0))
  (global $MonoWebCilDescriptorLength (export "MonoWebCilDescriptorLength") i32 (i32.const 2))
  (data $MonoWebCilDescriptor "\01\02")
  (global $MonoWebCilModule0Length i32 (i32.const 4))
  (data $MonoWebCilModule0 "MZ\01\02")
  (func (export "MonoWebCilGetDescriptor") (param i32 i32) (result)
    (if (i32.eq (local.get 1) (global.get $MonoWebCilDescriptorLength))
      (memory.init $MonoWebCilDescriptor
        (local.get 0) ;;  dest
        (i32.const 0) ;; src
        (global.get $MonoWebCilDescriptorLength) ;; n
      )
    )
  )
  (func (export "MonoWebCilGetModuleData") (param i32 i32 i32) (result)
    (if (i32.eq (local.get 0) (i32.const 0))
      (if (i32.eq (local.get 2) (global.get $MonoWebCilModule0Length))
        (memory.init $MonoWebCilModule0 
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
