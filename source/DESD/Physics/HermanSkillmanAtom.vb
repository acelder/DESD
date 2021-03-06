﻿''' <summary>
''' Represents an atom, its electronic wave functions, and
''' its total potential.  Self-consistent atomic wave functions and potential are calculated using the method
''' of Herman and Skillman.
''' </summary>
Public Class HermanSkillmanAtom
	
	Implements IAtom
	
    Private mZ As Element
    Private mConfiguration As ElectronicConfiguration
    
	Private mOrbitals As New List(Of Orbital)
	Private mPotential as Double()
	
	Private mMesh As HermanSkillmanMesh
	
	Private mPotentialSansExchange As Double()
	Private mRho As Double()
	
	
	Public Enum ExchangeMode
		NoExchange = 0
		NonStatisticalExchange = 1
		StatisticalExchange = 2
	End Enum

	
#Region "Constructors"

    ''' <summary>
    ''' Create an atom with the ground state configuration.
    ''' </summary>
    ''' <param name="atomicNumber"></param>
    ''' <remarks></remarks>
    Sub New(ByVal atomicNumber As Element)
        mZ = atomicNumber
        mConfiguration = New ElectronicConfiguration(atomicNumber)
        Solve()
    End Sub

    Sub New(ByVal Z as integer)
        mZ = ctype(Z,element)
        mConfiguration = New ElectronicConfiguration(mZ)
        Solve()
    End Sub
    
    
    Sub New(ByVal Z as integer, configuration as string)
        mZ = ctype(Z,element)
        mConfiguration = New ElectronicConfiguration(configuration)
        Solve()
    End Sub
    
        
    Sub New(ByVal atomicNumber As Element, ByVal configuration As ElectronicConfiguration)
        mZ = atomicNumber
        mConfiguration = configuration
        Solve()
    End Sub
    
    

#End Region


#Region "Properties"

		''' <summary>
		''' Returns the atomic number (number of protons) of the atom.
		''' </summary>
        Public ReadOnly Property AtomicNumber() As Integer Implements IAtom.AtomicNumber
            Get
                Return CInt(mZ)
            End Get
        End Property

		''' <summary>
		''' Returns the value of the Element enumeration corresponding to the chemical element of the atom.
		''' </summary>
        Public ReadOnly Property Element() As Element Implements IAtom.Element
            Get
                Return mZ
            End Get
        End Property


		Public ReadOnly Property Mesh As IRadialMesh Implements IAtom.Mesh
			Get
				Return mMesh
			end Get
		End Property

		Public ReadOnly Property Configuration As ElectronicConfiguration Implements IAtom.Configuration
			Get
				Return mConfiguration
			End Get
		End Property
		
		Public ReadOnly Property Orbitals As List(Of Orbital)
			Get
				return mOrbitals
			End Get
        End Property

        Public ReadOnly Property GetOrbital(ByVal n As Integer, ByVal l As Integer) As Orbital
            Get
                Dim orb = From o In mOrbitals Where (o.N = n) And (o.L = l) Take 1
                Return orb.Single
            End Get
        End Property
		
		Public ReadOnly Property Potential As Double() Implements IAtom.Potential
			Get
				Dim retval(mPotential.Length-1) as Double
				system.Array.Copy(mPotential,retval,mPotential.Length)
				Return retval
			End Get
		End Property
		
		Public ReadOnly Property PotentialSansExchange As Double()
			Get
				Dim retval(mPotentialSansExchange.Length-1) as Double
				system.Array.Copy(mPotentialSansExchange,retval,mPotentialSansExchange.Length)
				Return retval
			End Get
		End Property
		
		''' <summary>
		''' The spherically averaged total electronic charge density, tabulated on the radial mesh.
		''' </summary>
		Public ReadOnly Property Rho As Double() Implements IAtom.Rho
			Get
				Dim retval(mRho.Length-1) as Double
				system.Array.Copy(mRho,retval,mRho.Length)
				Return retval
			End Get
		End Property
		
#End Region


#Region "Private Methods"

        ''' <summary>
        ''' Solves for the atomic potential and electronic radial wavefunctions using the method of Herman and Skillman
        ''' </summary>
        ''' <remarks></remarks>
        Private Sub Solve()
        	
        	'// Set up the mesh:
        	mMesh = New HermanSkillmanMesh(mZ, HermanSkillmanMeshSize.Large)

        	
        	'// First, get a list of the (n,l) values and occupancies from the Configuration:
        	Dim NOrbitals As Integer = mConfiguration.OrbitalCount
        	Dim OrbitalQNs(,) As Integer = mConfiguration.GetQuantumNumbers
        	        	
			Dim iMax As Integer = mMesh.Count-1
			
			Dim Vtrial() As Double 
			Dim Vnew(iMax) As Double
			Dim Solution As Orbital
			Dim N As Integer
			Dim L As Integer
			Dim Occupancy as Double
            Dim Converged As Boolean = False
            Dim DeltaV as Double
            Dim DeltaVMax as Double
            Dim DeltaVtolerance As Double = 0.0001
			Dim nIterations As Integer = 0
			
            
            Do Until Converged
            
            	'// 1) Create the trial potential
                If (Vtrial Is Nothing) Then
                    '// This is the first loop, create an initial trial potential by looking up one from the table.
                    Vtrial = GetStartingPotential(mZ, mMesh)
                Else
                    '// Construct the new trial potential from the average of the previous trial potential
                    '// and the new potential.
                    For i As Integer = 0 To iMax
                        Vtrial(i) = (Vtrial(i) + Vnew(i)) / 2.0
                    Next
                End If

            	'// 2) Solve for electronic wave functions and energies for all orbitals in the configuration

            	'// First, clear out any existing orbitals:
            	mOrbitals.clear
            	
            	For i As Integer = 0 To NOrbitals - 1
					N = OrbitalQNs(i,0)
					L = OrbitalQNs(i,1)
					Occupancy = mConfiguration.Occupancy(N,L)
					Solution = BoundRSESOlver.Solve(mMesh,N,L,Occupancy,Vtrial)
					mOrbitals.Add(Solution)
				Next
				
				
            	'// 3) Compute new potential from the solution orbitals
                Vnew = HermanSkillmanPotential.GetPotential(mMesh, mZ, mOrbitals, ExchangeMode.NonStatisticalExchange,true)
				
            	'// 4) Check for convergence
            	DeltaVMax = 0.0
            	For i As Integer = 0 To iMax
            		DeltaV = system.Math.Abs(Vnew(i) - Vtrial(i)) / System.Math.Abs(Vnew(i) + Vtrial(i))
            		if DeltaV > DeltaVMax then DeltaVMax = DeltaV
            	Next
            	If DeltaVMax < DeltaVTolerance Then Converged = True
            	
            	nIterations += 1
            	
            	If nIterations > 200 Then
            		'throw new Exception("Failed to converge in Atom.Solve for N = " & mZ.tostring)
            		exit do
            	End If
            	
            Loop
            
            '// We've converged on a self-consistent potential.
            mPotential = Vnew

			'// Do one more calculation to get the potential sans exchange and the charge density:
			Dim details as AtomicPotential = HermanSkillmanPotential.GetPotentialDetails(mMesh, mZ, mOrbitals, ExchangeMode.NoExchange, True)

			mPotentialSansExchange = details.V
			mRho = details.Rho
		
        End Sub
        
        
        Public Shared Function GetPotential(ByVal mesh As IRadialMesh, ByVal Z As Element, ByVal orbitals As IList(Of Orbital), ByVal exchange As ExchangeMode, byval useLatterTail as boolean) As Double()

            '// Dimension arrays
            Dim iMax As Integer = mesh.Count - 1
            Dim r As Double() = mesh.GetArray()     '// An array for radial r values

            '// Compute the total number of electrons and the total radial charge density Sigma(),

            '// Initialize the number of electrons as zero.  We'll accumulate the total
            '// occupancy as we enumerate through the electron orbitals.
            Dim nE As Double = 0.0

            '// Go through the list of electron orbitals and accumulate the total
            '// number of electrons and the the total radial charge density.
            '// The first point of Sigma is zero by definition,
            '// since Pnl(0) = 0.0
            Dim Sigma(iMax) As Double
            For Each orb As Orbital In orbitals
                nE += orb.Occupancy
                For i As Integer = 0 To iMax
                    'Sigma(i) += -orb.Occupancy * orb.P(i) ^ 2
                    Sigma(i) += orb.sigma(i)
                Next
            Next
			'Console.WriteLine("Number of electrons in HSP is " & nE.ToString)
			
            '// Compute the integral of Sigma from 0 to r -
            '// This will be used later to compute Term 2 of the potential.
            'Dim Term2 As Double() = Sieger.Math.Integration.SimpsonsRule.NonuniformArray(r, Sigma)
        Dim Term2 As Double() = DESD.Math.Integration.TrapezoidalRuleIntegrator.IntegrateArray(r, Sigma)


            '// Compute Rho, Term1 and the intermediate array for Term3, SigmaOverR():
            Dim Rho(iMax) As Double
            Dim Term1(iMax) As Double               '// Array for -2Z/r, the nuclear coulomb term.
            Dim SigmaOverR(iMax) As Double
            Dim VExchange(iMax) As Double           '// Array for the exchange potential.

            '// Get the statistical exchange parameter alpha, if called for
            Dim alpha As Double = 1.0
            If exchange = ExchangeMode.StatisticalExchange Then alpha = StatisticalAlpha(Z)

            '// Set the values at the origin:
            Rho(0) = 0.0
            Term1(0) = Double.NegativeInfinity  '// We won't use this value, so it's kind of academic.
            SigmaOverR(0) = 0.0

            '// Define convenience values
            Dim OneOver4Pi As Double = 1.0 / (4.0 * System.Math.PI)
            Dim ThreeOver8Pi As Double = 3.0 / (8.0 * System.Math.PI)
            Dim OneThird As Double = 1.0 / 3.0

            For i As Integer = 1 To iMax
                Term1(i) = -2.0 * Z / r(i)
                SigmaOverR(i) = Sigma(i) / r(i)
                Rho(i) = OneOver4Pi * Sigma(i) / r(i) ^ 2
                If exchange = exchangemode.NoExchange Then
                	VExchange(i) = 0.0
                Else
                	VExchange(i) = -6.0 * alpha * System.Math.Pow(System.Math.Abs(ThreeOver8Pi * System.Math.Abs(Rho(i))), OneThird)	
                End If
            Next

            '// Now compute the integral of SigmaOverR from 0 to r:
            'Dim Term3 As Double() = Sieger.Math.Integration.SimpsonsRule.NonuniformArray(r, SigmaOverR)
        Dim Term3 As Double() = DESD.Math.Integration.TrapezoidalRuleIntegrator.IntegrateArray(r, SigmaOverR)

            '// Now we're ready to put it all together and compute the potential:

            '// Define the potential array:
            Dim V(iMax) As Double

            '// We'll set the first value to Double.NegativeInfinity
            V(0) = Double.NegativeInfinity

            '// Compute V at the switching radius for Latter tail:
            '// if r * V is greater than this value, we should replace it with the latter tail.
            Dim RVLatter As Double
            Dim iLatter As Integer = 1
			If useLatterTail Then
             	RVLatter  = -2.0 * (CDbl(Z) - nE + 1.0)
			Else
             	RVLatter  = double.PositiveInfinity
			End If

            '// Now compute the potential:
            Dim Temp As Double
            For i As Integer = 1 To iMax
                Temp = Term1(i) - 2.0 * Term2(i) / r(i) - 2.0 * (Term3(iMax) - Term3(i)) + VExchange(i)
                If r(i) * Temp > RVLatter Then Exit For
                V(i) = Temp
                iLatter += 1
            Next

			'// Purely diagnostic
'			If Not(uselattertail) Then
'				Console.WriteLine("R, V, Term1, Term2, Term3, Vexchange")
'				For i As Integer = 1 To iMax
'					Console.WriteLine(r(i).ToString & ", " & V(i).ToString & ", " & Term1(i).ToString & ", " & Term2(i).ToString & ", " & Term3(i).ToString & ", " & VExchange(i).ToString)
'	            Next
'
'			End If
			'// OK, so the issue is instability in term2 and term3.  The bug comes from Term3(iMax) - Term3(i)
			
            '// Fill out the rest of the potential with the Latter tail:
            'Console.WriteLine("Ilatter = " & ilatter.ToString & " and iMax = " & iMax.ToString)
            For i As Integer = iLatter To iMax
                V(i) = (-2.0 * (CDbl(Z) - nE + 1.0)) / r(i)
            Next

            Return V

        End Function


    Public Shared Function GetStartingPotential(ByVal Z As Element, ByVal mesh As IRadialMesh) As Double()

        '// Strategy:  Starting potentials are tabulated on a 110 point mesh
        '// Start with the potential from the highest available given.
        '// Use CubicSpline to fill out the normalized potential U on the new mesh,
        '// and then convert to V

        Dim U() As Double
        
        '// Get a mesh for the given Z:
        '// Points below are tabulated on every 4th point of this mesh
        '// Need to enable an abridged mesh in HermanSkillmanMesh.
        Dim refMesh As IRadialMesh = New HermanSkillmanMesh(Z, HermanSkillmanMeshSize.Normal)
        

        Select Case Z
            Case Is <= Element.Helium
                Dim U0() As Double = {1.0, 0.99609, 0.99205, 0.98787, 0.98357, 0.97916, 0.97464, 0.97002, 0.96531, 0.96053, _
                                      0.95566, 0.94574, 0.93558, 0.92524, 0.91477, 0.90418, 0.89352, 0.88282, 0.8721, 0.86139, _
                                      0.85071, 0.82949, 0.80858, 0.78807, 0.76804, 0.74854, 0.72961, 0.71127, 0.69354, 0.67642, _
                                      0.65991, 0.6287, 0.59981, 0.57311, 0.54844, 0.52566, 0.50461, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, _
                                      0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, _
                                      0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, _
                                      0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, _
                0.5, 0.5, 0.5}
                U = U0
                
            Case Is <= Element.Carbon
                Dim U0() As Double = {1.0, 0.99384, 0.98748, 0.98095, 0.97428, 0.9675, 0.96064, 0.95371, 0.94674, 0.93974, _
                                      0.93273, 0.91873, 0.90483, 0.89111, 0.8776, 0.86436, 0.85141, 0.83876, 0.82642, 0.8144, _
                                      0.80269, 0.7802, 0.75887, 0.7386, 0.71929, 0.70084, 0.68315, 0.66616, 0.64979, 0.634, _
                                      0.61874, 0.58976, 0.56278, 0.53786, 0.51509, 0.49448, 0.47596, 0.4593, 0.4442, 0.43038, _
                                      0.41755, 0.3941, 0.37273, 0.35292, 0.33441, 0.31711, 0.30095, 0.28586, 0.2718, 0.25871, _
                                      0.24652, 0.22462, 0.20558, 0.18896, 0.17441, 0.16667, 0.16667, 0.16667, 0.16667, 0.16667, _
                                      0.16667, 0.16667, 0.16667, 0.16667, 0.16667, 0.16667, 0.16667, 0.16667, 0.16667, 0.16667, _
                                      0.16667, 0.16667, 0.16667, 0.16667, 0.16667, 0.16667, 0.16667, 0.16667, 0.16667, 0.16667, _
                                      0.16667, 0.16667, 0.16667, 0.16667, 0.16667, 0.16667, 0.16667, 0.16667, 0.16667, 0.16667, _
                                      0.16667, 0.16667, 0.16667, 0.16667, 0.16667, 0.16667, 0.16667, 0.16667, 0.16667, 0.16667, _
                0.16667, 0.16667, 0.16667, 0.16667, 0.16667, 0.16667, 0.16667, 0.16667, 0.16667, 0.16667}
                U = U0
            Case Is <= Element.Nitrogen
                Dim U0() As Double = {1.00000, .99337, .98653, .97952, .97238, .96513, .95781, .95043, .94302, .93560, _
  .92818, .91341, .89880, .88443, .87033, .85654, .84308, .82996, .81718, .80474, _
  .79263, .76934, .74722, .72614, .70601, .68671, .66817, .65033, .63316, .61661, _
  .60069, .57073, .54333, .51849, .49609, .47585, .45741, .44042, .42458, .40965, _
  .39549, .36908, .34486, .32259, .30215, .28341, .26628, .25061, .23629, .22319, _
  .21120, .19011, .17224, .15696, .14380, .14286, .14286, .14286, .14286, .14286, _
  .14286, .14286, .14286, .14286, .14286, .14286, .14286, .14286, .14286, .14286, _
  .14286, .14286, .14286, .14286, .14286, .14286, .14286, .14286, .14286, .14286, _
  .14286, .14286, .14286, .14286, .14286, .14286, .14286, .14286, .14286, .14286, _
  .14286, .14286, .14286, .14286, .14286, .14286, .14286, .14286, .14286, .14286, _
                .14286, .14286, .14286, .14286, .14286, .14286, .14286, .14286, .14286, .14286}
                U = U0

            Case Is <= Element.Oxygen
                Dim U0() As Double = {1.00000, .99292, .98562, .97815, .97054, .96284, .95508, .94727, .93944, .93162, _
  .92381, .90830, .89301, .87800, .86332, .84898, .83500, .82139, .80814, .79523, _
  .78266, .75848, .73546, .71349, .69245, .67227, .65289, .63428, .61641, .59929, _
  .58292, .55242, .52485, .49995, .47731, .45652, .43719, .41906, .40194, .38569, _
  .37025, .34156, .31558, .29212, .27098, .25196, .23486, .21948, .20563, .19313, _
  .18182, .16223, .14590, .13214, .12500, .12500, .12500, .12500, .12500, .12500, _
  .12500, .12500, .12500, .12500, .12500, .12500, .12500, .12500, .12500, .12500, _
  .12500, .12500, .12500, .12500, .12500, .12500, .12500, .12500, .12500, .12500, _
  .12500, .12500, .12500, .12500, .12500, .12500, .12500, .12500, .12500, .12500, _
  .12500, .12500, .12500, .12500, .12500, .12500, .12500, .12500, .12500, .12500, _
                .12500, .12500, .12500, .12500, .12500, .12500, .12500, .12500, .12500, .12500}
                U = U0
            Case Is <= Element.Flourine
                Dim U0() As Double = {1.00000, .99247, .98473, .97681, .96877, .96063, .95244, .94422, .93599, .92778, _
  .91959, .90337, .88742, .87179, .85652, .84163, .82713, .81301, .79926, .78587, _
  .77282, .74769, .72373, .70083, .67890, .65788, .63775, .61848, .60007, .58252, _
  .56583, .53490, .50694, .48145, .45798, .43612, .41561, .39628, .37801, .36073, _
  .34438, .31434, .28759, .26384, .24280, .22416, .20764, .19297, .17990, .16823, _
  .15777, .13984, .12508, .11276, .11111, .11111, .11111, .11111, .11111, .11111, _
  .11111, .11111, .11111, .11111, .11111, .11111, .11111, .11111, .11111, .11111, _
  .11111, .11111, .11111, .11111, .11111, .11111, .11111, .11111, .11111, .11111, _
  .11111, .11111, .11111, .11111, .11111, .11111, .11111, .11111, .11111, .11111, _
  .11111, .11111, .11111, .11111, .11111, .11111, .11111, .11111, .11111, .11111, _
                .11111, .11111, .11111, .11111, .11111, .11111, .11111, .11111, .11111, .11111}
                U = U0
            Case Is <= Element.Magnesium
                Dim U0() As Double = {1.00000, .99170, .98317, .97448, .96567, .95680, .94790, .93900, .93012, .92130, _
  .91254, .89528, .87841, .86197, .84598, .83043, .81532, .80062, .78632, .77240, _
  .75884, .73274, .70791, .68430, .66187, .64063, .62053, .60155, .58360, .56662, _
  .55050, .52049, .49296, .46750, .44387, .42190, .40148, .38251, .36492, .34861, _
  .33349, .30650, .28323, .26306, .24541, .22983, .21594, .20345, .19215, .18188, _
  .17251, .15626, .14301, .13248, .12425, .11781, .11263, .10829, .10449, .10105, _
  .09784, .09191, .08646, .08333, .08333, .08333, .08333, .08333, .08333, .08333, _
  .08333, .08333, .08333, .08333, .08333, .08333, .08333, .08333, .08333, .08333, _
  .08333, .08333, .08333, .08333, .08333, .08333, .08333, .08333, .08333, .08333, _
  .08333, .08333, .08333, .08333, .08333, .08333, .08333, .08333, .08333, .08333, _
                .08333, .08333, .08333, .08333, .08333, .08333, .08333, .08333, .08333, .08333}
                U = U0
            Case Is <= Element.Aluminum
                  Dim U0() As Double = {1.00000, .99157, .98291, .97408, .96515, .95616, .94715, .93815, .92919, .92029, _
  .91147, .89411, .87717, .86069, .84468, .82913, .81402, .79934, .78506, .77117, _
  .75764, .73164, .70696, .68357, .66145, .64058, .62091, .60236, .58483, .56823, _
  .55245, .52300, .49595, .47096, .44784, .42643, .40663, .38833, .37142, .35578, _
  .34132, .31549, .29313, .27356, .25623, .24074, .22677, .21412, .20267, .19233, _
  .18305, .16746, .15534, .14583, .13807, .13142, .12547, .11999, .11487, .11003, _
  .10546, .09701, .08941, .08261, .07692, .07692, .07692, .07692, .07692, .07692, _
  .07692, .07692, .07692, .07692, .07692, .07692, .07692, .07692, .07692, .07692, _
  .07692, .07692, .07692, .07692, .07692, .07692, .07692, .07692, .07692, .07692, _
  .07692, .07692, .07692, .07692, .07692, .07692, .07692, .07692, .07692, .07692, _
                  .07692, .07692, .07692, .07692, .07692, .07692, .07692, .07692, .07692, .07692}
                U = U0
          	
            Case Is <= Element.Silicon
                Dim U0() As Double = {1.0, 0.99144, 0.98265, 0.97369, 0.96463, 0.95552, 0.94641, 0.93731, 0.92827, 0.91929, _
                                      0.91041, 0.89294, 0.87593, 0.8594, 0.84335, 0.82778, 0.81266, 0.79798, 0.7837, 0.76982, _
                                      0.75631, 0.73038, 0.70584, 0.68267, 0.66083, 0.64028, 0.62096, 0.60274, 0.58551, 0.56917, _
                                      0.55361, 0.52452, 0.49779, 0.47316, 0.45043, 0.42947, 0.41014, 0.39232, 0.37587, 0.36066, _
                                      0.34657, 0.32127, 0.29915, 0.27955, 0.26199, 0.24617, 0.23189, 0.21905, 0.20758, 0.19743, _
                                      0.18849, 0.17369, 0.16187, 0.15193, 0.14314, 0.13516, 0.12779, 0.12094, 0.11457, 0.10864, _
                                      0.10311, 0.09317, 0.08454, 0.07704, 0.07143, 0.07143, 0.07143, 0.07143, 0.07143, 0.07143, _
                                      0.07143, 0.07143, 0.07143, 0.07143, 0.07143, 0.07143, 0.07143, 0.07143, 0.07143, 0.07143, _
                                      0.07143, 0.07143, 0.07143, 0.07143, 0.07143, 0.07143, 0.07143, 0.07143, 0.07143, 0.07143, _
                                      0.07143, 0.07143, 0.07143, 0.07143, 0.07143, 0.07143, 0.07143, 0.07143, 0.07143, 0.07143, _
                0.07143, 0.07143, 0.07143, 0.07143, 0.07143, 0.07143, 0.07143, 0.07143, 0.07143, 0.07143}
                U = U0
            Case Is <= Element.Sulfur
                  Dim U0() As Double = { 1.00000, .99119, .98213, .97292, .96363, .95429, .94497, .93569, .92648, .91735, _
  .90834, .89066, .87348, .85683, .84069, .82504, .80987, .79514, .78083, .76694, _
  .75345, .72764, .70335, .68056, .65920, .63915, .62028, .60246, .58556, .56947, _
  .55413, .52542, .49910, .47494, .45274, .43233, .41353, .39615, .38004, .36504, _
  .35102, .32546, .30266, .28220, .26389, .24764, .23335, .22083, .20980, .19995, _
  .19102, .17509, .16102, .14837, .13695, .12664, .11736, .10901, .10148, .09470, _
  .08858, .07799, .06921, .06250, .06250, .06250, .06250, .06250, .06250, .06250  , _
  .06250, .06250, .06250, .06250, .06250, .06250, .06250, .06250, .06250, .06250, _
  .06250, .06250, .06250, .06250, .06250, .06250, .06250, .06250, .06250, .06250, _
  .06250, .06250, .06250, .06250, .06250, .06250, .06250, .06250, .06250, .06250, _
                  .06250, .06250, .06250, .06250, .06250, .06250, .06250, .06250, .06250, .06250}
                U = U0
          	
            Case Is <= Element.Titanium
                        Dim U0() As Double = {1.00000, .99064, .98104, .97131, .96152, .95175, .94203, .93241, .92291, .91354, _
  .90431, .88632, .86894, .85215, .83593, .82025, .80509, .79044, .77628, .76261, _
  .74942, .72444, .70120, .67951, .65916, .63996, .62179, .60453, .58811, .57247, _
  .55756, .52975, .50432, .48095, .45933, .43922, .42041, .40279, .38626, .37078, _
  .35633, .33040, .30794, .28819, .27043, .25420, .23923, .22539, .21258, .20074, _
  .18981, .17043, .15391, .13977, .12759, .11703, .10783, .09981, .09283, .08676, _
  .08151, .07305, .06669, .06174, .05772, .05430, .05129, .04858, .04611, .04545, _
  .04545, .04545, .04545, .04545, .04545, .04545, .04545, .04545, .04545, .04545, _
  .04545, .04545, .04545, .04545, .04545, .04545, .04545, .04545, .04545, .04545, _
  .04545, .04545, .04545, .04545, .04545, .04545, .04545, .04545, .04545, .04545, _
                        .04545, .04545, .04545, .04545, .04545, .04545, .04545, .04545, .04545, .04545}

                U = U0
            Case Is <= ELement.Nickel
            	
    	                        Dim U0() As Double = { 1.00000, .99009, .97993, .96966, .95938, .94915, .93903, .92903, .91919, .90951, _
  .90001, .88151, .86367, .84648, .82988, .81388, .79844, .78357, .76925, .75546, _
  .74218, .71702, .69350, .67135, .65040, .63052, .61161, .59359, .57640, .55998, _
  .54427, .51478, .48754, .46226, .43874, .41686, .39653, .37765, .36013, .34379, _
  .32850, .30054, .27547, .25289, .23254, .21427, .19789, .18325, .17015, .15844, _
  .14795, .13008, .11553, .10355, .09358, .08521, .07815, .07217, .06707, .06270, _
  .05894, .05282, .04804, .04418, .04095, .03820, .03580, .03571, .03571, .03571, _
  .03571, .03571, .03571, .03571, .03571, .03571, .03571, .03571, .03571, .03571, _
  .03571, .03571, .03571, .03571, .03571, .03571, .03571, .03571, .03571, .03571, _
  .03571, .03571, .03571, .03571, .03571, .03571, .03571, .03571, .03571, .03571, _
    	                        .03571, .03571, .03571, .03571, .03571, .03571, .03571, .03571, .03571, .03571}

                U = U0

            Case Is <= Element.Copper
                Dim U0() As Double = {1.0, 0.98997, 0.97969, 0.96931, 0.95892, 0.94859, 0.93837, 0.92828, 0.91835, 0.90859, _
                                      0.89901, 0.88036, 0.86238, 0.84504, 0.82832, 0.81218, 0.79663, 0.78165, 0.76722, 0.75333, _
                                      0.73994, 0.71456, 0.69077, 0.66835, 0.64711, 0.62693, 0.60771, 0.58939, 0.57189, 0.55517, _
                                      0.53915, 0.50905, 0.4812, 0.45534, 0.43128, 0.40892, 0.38816, 0.36888, 0.35093, 0.33416, _
                                      0.31841, 0.28955, 0.26368, 0.24044, 0.21962, 0.20102, 0.18445, 0.16972, 0.15664, 0.14501, _
                                      0.13467, 0.11722, 0.10322, 0.09185, 0.08251, 0.07475, 0.06825, 0.06276, 0.05807, 0.05406, _
                                      0.05058, 0.04492, 0.04052, 0.03703, 0.03448, 0.03448, 0.03448, 0.03448, 0.03448, 0.03448, _
                                      0.03448, 0.03448, 0.03448, 0.03448, 0.03448, 0.03448, 0.03448, 0.03448, 0.03448, 0.03448, _
                                      0.03448, 0.03448, 0.03448, 0.03448, 0.03448, 0.03448, 0.03448, 0.03448, 0.03448, 0.03448, _
                                      0.03448, 0.03448, 0.03448, 0.03448, 0.03448, 0.03448, 0.03448, 0.03448, 0.03448, 0.03448, _
                0.03448, 0.03448, 0.03448, 0.03448, 0.03448, 0.03448, 0.03448, 0.03448, 0.03448, 0.03448}

                U = U0
            Case Is <= Element.Zinc
            	Dim U0() As Double = {1.00000, .98990, .97956, .96912, .95867, .94829, .93802, .92789, .91793, .90814, _
  .89853, .87983, .86182, .84445, .82769, .81154, .79598, .78099, .76657, .75268, _
  .73930, .71390, .69009, .66761, .64631, .62607, .60679, .58840, .57085, .55406, _
  .53797, .50773, .47976, .45380, .42969, .40733, .38660, .36736, .34944, .33267, _
  .31692, .28805, .26225, .23919, .21864, .20039, .18422, .16990, .15721, .14596, _
  .13595, .11904, .10541, .09427, .08507, .07740, .07095, .06551, .06088, .05692, _
  .05350, .04792, .04355, .04001, .03706, .03454, .03333, .03333, .03333, .03333, _
  .03333, .03333, .03333, .03333, .03333, .03333, .03333, .03333, .03333, .03333, _
  .03333, .03333, .03333, .03333, .03333, .03333, .03333, .03333, .03333, .03333, _
  .03333, .03333, .03333, .03333, .03333, .03333, .03333, .03333, .03333, .03333, _
            	.03333, .03333, .03333, .03333, .03333, .03333, .03333, .03333, .03333, .03333}
                  U = U0

            Case Is <= Element.Molybdenum
              	Dim U0() As Double = {1.00000, .98936, .97849, .96757, .95672, .94599, .93544, .92509, .91494, .90499, _
  .89525, .87636, .85823, .84083, .82413, .80814, .79280, .77807, .76389, .75022, _
  .73700, .71177, .68797, .66545, .64409, .62381, .60451, .58612, .56858, .55183, _
  .53584, .50600, .47881, .45398, .43119, .41018, .39072, .37266, .35590, .34033, _
  .32585, .29984, .27715, .25715, .23935, .22339, .20903, .19613, .18454, .17412, _
  .16471, .14825, .13411, .12171, .11073, .10102, .09242, .08482, .07810, .07215, _
  .06687, .05800, .05094, .04527, .04067, .03692, .03382, .03124, .02906, .02719, _
  .02558, .02381, .02381, .02381, .02381, .02381, .02381, .02381, .02381, .02381, _
  .02381, .02381, .02381, .02381, .02381, .02381, .02381, .02381, .02381, .02381, _
  .02381, .02381, .02381, .02381, .02381, .02381, .02381, .02381, .02381, .02381, _
              	.02381, .02381, .02381, .02381, .02381, .02381, .02381, .02381, .02381, .02381}
                  U = U0
          	
                'Case Is <= Element.Hafnium
            Case Else

                Dim U0() As Double = {1.0, 0.98851, 0.97685, 0.96525, 0.95384, 0.94266, 0.93175, 0.92109, 0.91069, 0.90054, _
     0.89062, 0.87151, 0.85332, 0.83598, 0.8194, 0.80347, 0.78813, 0.77332, 0.75899, 0.74512, _
     0.73167, 0.70599, 0.68178, 0.65894, 0.63737, 0.61699, 0.59772, 0.57947, 0.56214, 0.54565, _
     0.52993, 0.50051, 0.47351, 0.44861, 0.42554, 0.40407, 0.38405, 0.36536, 0.3479, 0.3316, _
     0.31636, 0.28866, 0.26405, 0.24209, 0.22247, 0.20501, 0.18947, 0.17567, 0.16337, 0.15239, _
     0.14255, 0.12573, 0.11198, 0.10063, 0.09117, 0.08317, 0.07632, 0.07038, 0.06517, 0.06056, _
     0.05645, 0.04946, 0.04375, 0.03903, 0.03508, 0.03178, 0.02901, 0.0267, 0.02476, 0.02313, _
     0.02175, 0.01952, 0.01778, 0.01634, 0.0151, 0.01402, 0.01389, 0.01389, 0.01389, 0.01389, _
     0.01389, 0.01389, 0.01389, 0.01389, 0.01389, 0.01389, 0.01389, 0.01389, 0.01389, 0.01389, _
     0.01389, 0.01389, 0.01389, 0.01389, 0.01389, 0.01389, 0.01389, 0.01389, 0.01389, 0.01389, _
                0.01389, 0.01389, 0.01389, 0.01389, 0.01389, 0.01389, 0.01389, 0.01389, 0.01389, 0.01389}
                U = U0

                'V = HSTableConverter.ExpandU(VHelium, Z)
        End Select

		'// Construct the abridged R matrix from the reference mesh.
		Dim iShortMax as Integer = U.Length-1
		Dim abridgedR(iShortMax) As Double
		For i As Integer = 0 To U.Length-1
			abridgedR(i) = refMesh.R(i * 4)
		Next

		'// Now create a cubic spline for the (normalized) potential
        Dim CS As New DESD.Math.CubicSpline(abridgedR, U, 0.0, 0.0)
		
		'// Now fill in the return array on the input mesh points:
		Dim retval(mesh.Count-1) As Double
		
		For i As Integer = 0 To mesh.Count-1
			If mesh.R(i) > abridgedR(iShortMax) Then
				retval(i) = U(iShortMax)
			Else
				retval(i) = CS.Y(mesh.R(i))
			End If
		Next
		
		'// Now we should have in retval the normalized potential.
		'// Now de-normalize it for the given Z:
		retval(0) = double.NegativeInfinity
		For i As Integer = 1 To mesh.Count-1
			retval(i)= -2.0 * CDBL(Z) * retval(i) / mesh.R(i)
		Next
		
        Return retval

    End Function

        ''' <summary>
        ''' Statistical exchange parameter Alpha, taken from
        ''' http://hermes.phys.uwm.edu/projects/elecstruct/Alpha.StatExchange.html
        ''' We are using the HF value.  Only values out to Z = 41 are tabulated.
        ''' We fill in the matrix with Z = 0 as zero (not used), and Z = 1 as 1.0.
        ''' </summary>
        ''' <remarks></remarks>
        Public Shared Function StatisticalAlpha(ByVal Z As Integer) As Double
            Dim mAlpha As Double() = {0.0, 1.0, 0.77298, 0.78147, 0.76823, 0.76531, 0.75928, 0.75197, 0.74447, 0.73732, 0.73081, _
                                          0.73115, 0.72913, 0.72853, 0.72751, 0.7262, 0.72475, 0.72325, 0.72177, 0.72117, 0.71984, _
                                          0.71841, 0.71695, 0.71556, 0.71352, 0.71279, 0.71151, 0.71018, 0.70896, 0.70697, 0.70673, _
                                          0.7069, 0.70684, 0.70665, 0.70638, 0.70606, 0.70574, 0.70553, 0.70504, 0.70465, 0.70424, _
            0.70383}
            If Z <= 41 Then
                Return mAlpha(Z)
            Else
                Return mAlpha(41)
            End If
        End Function

#End Region


    End Class
