$types = @([Math],[double],[float],[decimal],[int],[long])
$methods = @('Abs','Sqrt','Exp','Log','Log10','Acos','Asin','Atan','Atan2','Cos','Sin','Tan','Sign','Cbrt','Floor','Ceiling','Round','Pow','RadiansToDegrees','DegreesToRadians')
foreach ($mn in $methods) {
	foreach ($t in $types) {
		$flags = [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Static
		$t.GetMethods($flags) | Where-Object { $_.Name -eq $mn } | ForEach-Object {
			$p = ($_.GetParameters() | ForEach-Object { $_.ParameterType.Name }) -join ', '
			"$($t.Name).$mn($p) -> $($_.ReturnType.Name)"
		}
	}
}
